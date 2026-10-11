using System.ComponentModel.DataAnnotations;

namespace AddressBook.Api.Dtos;

/// <summary>Ids of the contacts to delete (for example, the rows ticked with checkboxes).</summary>
public class BulkDeleteDto
{
    /// <example>[1, 2, 3]</example>
    [Required, MinLength(1), MaxLength(100)]
    public List<int> Ids { get; set; } = [];
}

/// <summary>How many contacts were actually deleted. Unknown ids are ignored.</summary>
public record BulkDeleteResult(int DeletedCount);
