using System.ComponentModel.DataAnnotations;
using AddressBook.Api.Common;

namespace AddressBook.Api.Dtos;

/// <summary>
/// Body for PATCH (partial edit). Only fields that are present (not null) change.
/// Send "" as Phone to clear it. To remove an email, use PUT.
/// </summary>
public class PatchContactDto : IValidatableObject
{
    /// <example>Alan</example>
    [MinLength(1), MaxLength(50), RegularExpression(@".*\S.*", ErrorMessage = "FirstName cannot be blank.")]
    public string? FirstName { get; set; }

    /// <example>Turing</example>
    [MinLength(1), MaxLength(50), RegularExpression(@".*\S.*", ErrorMessage = "LastName cannot be blank.")]
    public string? LastName { get; set; }

    /// <example>alan.turing@auning.com</example>
    [EmailAddress, MaxLength(100)]
    public string? Email { get; set; }

    /// <example>+1 555 123 4567</example>
    [MaxLength(30)]
    public string? Phone { get; set; }

    /// <summary>Replaces the whole tag list when present.</summary>
    [MaxLength(20)]
    public List<string>? Tags { get; set; }

    public bool? IsFavorite { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TagRules.Validate(Tags);
}
