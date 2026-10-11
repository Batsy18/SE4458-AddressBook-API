using AddressBook.Api.Dtos;

namespace AddressBook.Api.Services;

/// <summary>Business logic for contacts. Methods return null/false when the contact doesn't exist.</summary>
public interface IContactService
{
    Task<ContactDto?> GetAsync(int id);
    Task<ContactDto> CreateAsync(CreateContactDto dto);
}
