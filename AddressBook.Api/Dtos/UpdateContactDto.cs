namespace AddressBook.Api.Dtos;

/// <summary>
/// Body for PUT (full update). Same fields as create: every editable field is replaced,
/// so optional fields that are omitted are cleared and IsFavorite falls back to false.
/// </summary>
public class UpdateContactDto : CreateContactDto
{
}
