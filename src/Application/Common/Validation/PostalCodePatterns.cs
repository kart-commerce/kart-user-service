using System.Text.RegularExpressions;

namespace Kart.User.Application.Common.Validation;

/// <summary>
/// Country-aware format-only postal-code validation (edge-cases.md "Address write with
/// invalid/unvalidatable data" — required-field presence + a country-aware pattern check,
/// deliberately no synchronous geocoding provider). A handful of high-volume countries get a
/// real pattern; every other <c>countryCode</c> falls back to a lenient non-empty/length check
/// rather than a hard-coded pattern list this service would need to maintain for all ~250
/// ISO 3166 countries — a defensible engineering default, not full geocoding-grade validation.
/// </summary>
public static class PostalCodePatterns
{
    private static readonly IReadOnlyDictionary<string, Regex> ByCountryCode = new Dictionary<string, Regex>
    {
        ["US"] = new Regex(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled),
        ["CA"] = new Regex(@"^[A-Za-z]\d[A-Za-z] ?\d[A-Za-z]\d$", RegexOptions.Compiled),
        ["GB"] = new Regex(@"^[A-Za-z]{1,2}\d[A-Za-z\d]? ?\d[A-Za-z]{2}$", RegexOptions.Compiled),
    };

    public static bool IsValid(string countryCode, string postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return false;
        }

        return ByCountryCode.TryGetValue(countryCode.ToUpperInvariant(), out var pattern)
            ? pattern.IsMatch(postalCode.Trim())
            : postalCode.Trim().Length is >= 2 and <= 12;
    }
}
