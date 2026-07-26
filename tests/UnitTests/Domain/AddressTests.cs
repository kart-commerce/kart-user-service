using Kart.User.Domain.Entities;
using Kart.User.Domain.Enums;

namespace Kart.User.UnitTests.Domain;

public sealed class AddressTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReplaceWith_WholeRecordReplace_UpdatesAllFields()
    {
        var address = Address.Create("user-1", AddressType.Shipping, "1 First St", null, "Metropolis", null, "12345", "US", null, false, Now, "user-1");

        address.ReplaceWith(AddressType.Billing, "2 Second St", "Apt 4", "Gotham", "NY", "54321", "CA", "555-9999", true, Now.AddDays(1), "user-1");

        Assert.Equal(AddressType.Billing, address.Type);
        Assert.Equal("2 Second St", address.Line1);
        Assert.Equal("Apt 4", address.Line2);
        Assert.Equal("Gotham", address.City);
        Assert.True(address.IsDefault);
    }

    [Fact]
    public void Tombstone_ClearsPiiFields()
    {
        var address = Address.Create("user-1", AddressType.Shipping, "1 First St", "Apt 2", "Metropolis", null, "12345", "US", "555-1234", true, Now, "user-1");

        address.Tombstone(Now.AddDays(1));

        Assert.Equal("[erased]", address.Line1);
        Assert.Null(address.Line2);
        Assert.Null(address.Phone);
    }

    [Fact]
    public void ClearDefault_WhenAlreadyNotDefault_IsNoOpAndDoesNotBumpUpdatedAt()
    {
        var address = Address.Create("user-1", AddressType.Shipping, "1 First St", null, "Metropolis", null, "12345", "US", null, false, Now, "user-1");

        address.ClearDefault(Now.AddDays(1), "user-1");

        Assert.Equal(Now, address.UpdatedAt);
    }
}
