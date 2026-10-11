using AddressBook.Api.Common;
using AddressBook.Api.Data;
using AddressBook.Api.Dtos;
using AddressBook.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Services;

public class ContactService(AddressBookContext db) : IContactService
{
    public async Task<PagedResult<ContactDto>> ListAsync(ContactQuery query)
    {
        // The in-memory table is small, so we load it and filter in .NET.
        // That lets us use culture-independent, accent-insensitive matching (see TextMatching).
        IEnumerable<Contact> contacts = await db.Contacts.AsNoTracking().ToListAsync();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            contacts = contacts.Where(c => MatchesSearch(c, term));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            contacts = contacts.Where(c => c.Tags.Contains(tag, TextMatching.Comparer));
        }

        if (query.Favorite is bool favorite)
        {
            contacts = contacts.Where(c => c.IsFavorite == favorite);
        }

        var matches = Sort(contacts, query.SortBy, query.SortOrder).ToList();

        // long arithmetic: page * pageSize can overflow int for huge page numbers.
        var skip = (long)(query.Page - 1) * query.PageSize;
        List<ContactDto> items = skip >= matches.Count
            ? []
            : matches.Skip((int)skip).Take(query.PageSize).Select(ToDto).ToList();
        var totalPages = (int)Math.Ceiling(matches.Count / (double)query.PageSize);

        return new PagedResult<ContactDto>(items, query.Page, query.PageSize, matches.Count, totalPages);
    }

    public async Task<ContactDto?> GetAsync(int id)
    {
        var contact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return contact is null ? null : ToDto(contact);
    }

    public async Task<ContactDto> CreateAsync(CreateContactDto dto)
    {
        var now = DateTime.UtcNow;
        var contact = new Contact
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = Clean(dto.Email),
            Phone = Clean(dto.Phone),
            Tags = TagRules.Normalize(dto.Tags),
            IsFavorite = dto.IsFavorite,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return ToDto(contact);
    }

    public async Task<ContactDto?> UpdateAsync(int id, UpdateContactDto dto)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return null;
        }

        contact.FirstName = dto.FirstName.Trim();
        contact.LastName = dto.LastName.Trim();
        contact.Email = Clean(dto.Email);
        contact.Phone = Clean(dto.Phone);
        contact.Tags = TagRules.Normalize(dto.Tags);
        contact.IsFavorite = dto.IsFavorite;
        contact.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(contact);
    }

    public async Task<ContactDto?> PatchAsync(int id, PatchContactDto dto)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return null;
        }

        if (dto.FirstName is not null) contact.FirstName = dto.FirstName.Trim();
        if (dto.LastName is not null) contact.LastName = dto.LastName.Trim();
        if (dto.Email is not null) contact.Email = Clean(dto.Email);
        if (dto.Phone is not null) contact.Phone = Clean(dto.Phone);
        if (dto.Tags is not null) contact.Tags = TagRules.Normalize(dto.Tags);
        if (dto.IsFavorite is bool isFavorite) contact.IsFavorite = isFavorite;
        contact.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(contact);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return false;
        }

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids)
    {
        var idSet = ids.ToHashSet();
        var targets = await db.Contacts.Where(c => idSet.Contains(c.Id)).ToListAsync();

        db.Contacts.RemoveRange(targets);
        await db.SaveChangesAsync();
        return targets.Count;
    }

    public async Task<bool> SetFavoriteAsync(int id, bool isFavorite)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return false;
        }

        if (contact.IsFavorite != isFavorite)
        {
            contact.IsFavorite = isFavorite;
            contact.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync()
    {
        var tagLists = await db.Contacts.AsNoTracking().Select(c => c.Tags).ToListAsync();

        return tagLists
            .SelectMany(tags => tags)
            .Distinct(TextMatching.Comparer)
            .Order(TextMatching.Comparer)
            .ToList();
    }

    private static bool MatchesSearch(Contact c, string term) =>
        TextMatching.ContainsLoose(c.FirstName, term) ||
        TextMatching.ContainsLoose(c.LastName, term) ||
        TextMatching.ContainsLoose($"{c.FirstName} {c.LastName}", term) ||
        TextMatching.ContainsLoose(c.Email, term) ||
        c.Tags.Any(tag => TextMatching.ContainsLoose(tag, term));

    private static IEnumerable<Contact> Sort(IEnumerable<Contact> contacts, string sortBy, string sortOrder)
    {
        var descending = sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var comparer = TextMatching.Comparer;

        IOrderedEnumerable<Contact> ordered = sortBy.ToLowerInvariant() switch
        {
            "firstname" => descending
                ? contacts.OrderByDescending(c => c.FirstName, comparer)
                : contacts.OrderBy(c => c.FirstName, comparer),
            // Contacts without an email always go last.
            "email" => descending
                ? contacts.OrderBy(c => c.Email is null).ThenByDescending(c => c.Email, comparer)
                : contacts.OrderBy(c => c.Email is null).ThenBy(c => c.Email, comparer),
            "createdat" => descending
                ? contacts.OrderByDescending(c => c.CreatedAt)
                : contacts.OrderBy(c => c.CreatedAt),
            _ => descending
                ? contacts.OrderByDescending(c => c.LastName, comparer)
                : contacts.OrderBy(c => c.LastName, comparer),
        };

        // Ties are broken by Id so the order is stable between requests.
        return descending ? ordered.ThenByDescending(c => c.Id) : ordered.ThenBy(c => c.Id);
    }

    private static ContactDto ToDto(Contact c) => new(
        c.Id, c.FirstName, c.LastName, c.Email, c.Phone,
        c.Tags.ToList(), c.IsFavorite, c.CreatedAt, c.UpdatedAt);

    /// <summary>Trims optional text; blank becomes null.</summary>
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
