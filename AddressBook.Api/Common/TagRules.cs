using System.ComponentModel.DataAnnotations;

namespace AddressBook.Api.Common;

/// <summary>Validation and clean-up rules for contact tags.</summary>
public static class TagRules
{
    public const int MaxTagLength = 30;

    public static IEnumerable<ValidationResult> Validate(IEnumerable<string?>? tags)
    {
        if (tags is null)
        {
            yield break;
        }

        if (tags.Any(t => t is not null && t.Trim().Length > MaxTagLength))
        {
            yield return new ValidationResult(
                $"Each tag must be at most {MaxTagLength} characters.", ["Tags"]);
        }
    }

    /// <summary>Trims tags, drops blank ones and removes duplicates (ignoring case), keeping the first spelling.</summary>
    public static List<string> Normalize(IEnumerable<string?>? tags) =>
        (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .Distinct(TextMatching.Comparer)
            .ToList();
}
