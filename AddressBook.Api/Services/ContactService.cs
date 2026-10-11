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

    private static ContactDto ToDto(Contact c) => new(
        c.Id, c.FirstName, c.LastName, c.Email, c.Phone,
        c.Tags.ToList(), c.IsFavorite, c.CreatedAt, c.UpdatedAt);

    /// <summary>Trims optional text; blank becomes null.</summary>
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
