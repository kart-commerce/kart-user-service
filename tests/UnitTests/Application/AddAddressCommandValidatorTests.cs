using Kart.User.Application.Features.AddAddress;

namespace Kart.User.UnitTests.Application;

public sealed class AddAddressCommandValidatorTests
{
    private readonly AddAddressCommandValidator _validator = new();

    [Fact]
    public void Validate_WellFormedUsAddress_Passes()
    {
        var command = new AddAddressCommand("user-1", "user-1", "Shipping", "1 First St", null, "Metropolis", "NY", "12345", "US", null, true);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidPostalCodeForCountry_Fails()
    {
        var command = new AddAddressCommand("user-1", "user-1", "Shipping", "1 First St", null, "Metropolis", "NY", "not-a-zip", "US", null, false);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "postalCode");
    }

    [Fact]
    public void Validate_UnknownAddressType_Fails()
    {
        var command = new AddAddressCommand("user-1", "user-1", "NotAType", "1 First St", null, "Metropolis", "NY", "12345", "US", null, false);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MissingLine1_Fails()
    {
        var command = new AddAddressCommand("user-1", "user-1", "Shipping", "", null, "Metropolis", "NY", "12345", "US", null, false);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }
}
