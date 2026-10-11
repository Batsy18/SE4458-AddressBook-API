using System.Globalization;

namespace AddressBook.Api.Common;

/// <summary>
/// Text comparison rules for search, filtering and sorting. They use the invariant culture,
/// so results don't depend on the server's language settings. (On a Turkish machine,
/// "I".ToLower() is "ı", which breaks a naive ToLower() search.)
/// </summary>
public static class TextMatching
{
    /// <summary>Case-insensitive comparer for equality and sorting.</summary>
    public static readonly StringComparer Comparer = StringComparer.InvariantCultureIgnoreCase;

    private static readonly CompareInfo Invariant = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>True when <paramref name="value"/> contains <paramref name="term"/>, ignoring case and accents ("cagan" finds "Çağan").</summary>
    public static bool ContainsLoose(string? value, string term) =>
        value is not null &&
        Invariant.IndexOf(value, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
