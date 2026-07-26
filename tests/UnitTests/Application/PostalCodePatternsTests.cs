using Kart.User.Application.Common.Validation;

namespace Kart.User.UnitTests.Application;

public sealed class PostalCodePatternsTests
{
    [Theory]
    [InlineData("US", "12345", true)]
    [InlineData("US", "12345-6789", true)]
    [InlineData("US", "abcde", false)]
    [InlineData("CA", "K1A 0B1", true)]
    [InlineData("CA", "12345", false)]
    [InlineData("GB", "SW1A 1AA", true)]
    public void IsValid_KnownCountries_MatchesExpectedPattern(string countryCode, string postalCode, bool expected)
    {
        Assert.Equal(expected, PostalCodePatterns.IsValid(countryCode, postalCode));
    }

    [Theory]
    [InlineData("FR", "75001", true)]
    [InlineData("FR", "", false)]
    public void IsValid_UnknownCountry_FallsBackToLenientLengthCheck(string countryCode, string postalCode, bool expected)
    {
        Assert.Equal(expected, PostalCodePatterns.IsValid(countryCode, postalCode));
    }
}
