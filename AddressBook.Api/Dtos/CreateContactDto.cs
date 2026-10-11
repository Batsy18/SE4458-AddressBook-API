using System.ComponentModel.DataAnnotations;
using AddressBook.Api.Common;

namespace AddressBook.Api.Dtos;

/// <summary>Body for creating a contact. Id and timestamps are always set by the server.</summary>
public class CreateContactDto : IValidatableObject
{
    /// <summary>Required.</summary>
    /// <example>Alan</example>
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Required.</summary>
    /// <example>Turing</example>
    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Optional. Omit or send null for no email.</summary>
    /// <example>alan.turing@auning.com</example>
    [EmailAddress, MaxLength(100)]
    public string? Email { get; set; }

    /// <summary>Optional.</summary>
    /// <example>+1 555 123 4567</example>
    [MaxLength(30)]
    public string? Phone { get; set; }

    /// <summary>Free-text labels such as Work or Family. Up to 20 tags, each up to 30 characters.</summary>
    /// <example>["Work", "Tag"]</example>
    [MaxLength(20)]
    public List<string>? Tags { get; set; }

    /// <summary>Whether the contact is starred.</summary>
    public bool IsFavorite { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TagRules.Validate(Tags);
}
