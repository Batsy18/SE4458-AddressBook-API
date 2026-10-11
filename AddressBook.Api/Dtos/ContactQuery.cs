using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Dtos;

/// <summary>Search, filter, sort and paging options for listing contacts.</summary>
public class ContactQuery : IValidatableObject
{
    /// <summary>Text to find in first name, last name, full name, email or any tag. Ignores case and accents.</summary>
    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    /// <summary>Only contacts with exactly this tag (case-insensitive).</summary>
    [FromQuery(Name = "tag")]
    public string? Tag { get; set; }

    /// <summary>true = only favorites, false = only non-favorites, empty = all.</summary>
    [FromQuery(Name = "favorite")]
    public bool? Favorite { get; set; }

    /// <summary>firstName, lastName, email or createdAt. Contacts without email always come last.</summary>
    [FromQuery(Name = "sortBy")]
    public string SortBy { get; set; } = "lastName";

    /// <summary>asc or desc.</summary>
    [FromQuery(Name = "sortOrder")]
    public string SortOrder { get; set; } = "asc";

    /// <summary>Page number, starting at 1.</summary>
    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int Page { get; set; } = 1;

    /// <summary>Items per page, 1–100.</summary>
    [FromQuery(Name = "pageSize")]
    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;

    private static readonly string[] SortFields = ["firstName", "lastName", "email", "createdAt"];
    private static readonly string[] SortOrders = ["asc", "desc"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Ordinal comparison on purpose: a case-insensitive regex follows the server's culture,
        // and in Turkish "FIRSTNAME" doesn't match "firstName" (lowercase of I is ı there).
        if (!SortFields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "sortBy must be one of: firstName, lastName, email, createdAt.", ["sortBy"]);
        }

        if (!SortOrders.Contains(SortOrder, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("sortOrder must be asc or desc.", ["sortOrder"]);
        }
    }
}
