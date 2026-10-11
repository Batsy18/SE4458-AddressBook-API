using AddressBook.Api.Dtos;

namespace AddressBook.Api.Services;

/// <summary>Business logic for contacts. Methods return null/false when the contact doesn't exist.</summary>
public interface IContactService
{
    Task<ContactDto?> GetAsync(int id);
    Task<ContactDto> CreateAsync(CreateContactDto dto);
    Task<ContactDto?> UpdateAsync(int id, UpdateContactDto dto);
    Task<ContactDto?> PatchAsync(int id, PatchContactDto dto);
    Task<bool> DeleteAsync(int id);
    Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids);
}
