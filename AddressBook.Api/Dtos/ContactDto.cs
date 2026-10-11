namespace AddressBook.Api.Dtos;

/// <summary>A contact as returned by the API.</summary>
public record ContactDto(
    int Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Tags,
    bool IsFavorite,
    DateTime CreatedAt,
    DateTime UpdatedAt);
