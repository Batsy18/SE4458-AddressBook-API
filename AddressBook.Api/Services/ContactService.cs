using AddressBook.Api.Common;
using AddressBook.Api.Data;
using AddressBook.Api.Dtos;
using AddressBook.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Services;

public class ContactService(AddressBookContext db) : IContactService
{
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
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Order(StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    private static ContactDto ToDto(Contact c) => new(
        c.Id, c.FirstName, c.LastName, c.Email, c.Phone,
        c.Tags.ToList(), c.IsFavorite, c.CreatedAt, c.UpdatedAt);

    /// <summary>Trims optional text; blank becomes null.</summary>
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
