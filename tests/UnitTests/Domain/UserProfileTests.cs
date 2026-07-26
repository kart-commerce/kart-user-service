using Kart.User.Domain.Entities;
using Kart.User.Domain.Enums;
using Kart.User.Domain.ValueObjects;

namespace Kart.User.UnitTests.Domain;

public sealed class UserProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateFromRegistration_SeedsExpectedDefaults()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", "user@example.com", Now);

        Assert.Equal("user-1", profile.UserId);
        Assert.Equal("user@example.com", profile.ContactCopy.Email);
        Assert.Null(profile.ContactCopy.DisplayName);
        Assert.Equal(ErasureStatus.Active, profile.ErasureStatus);
        Assert.Empty(profile.Addresses);
        Assert.Equal("system:identity-registration-consumer", profile.CreatedBy);
    }

    [Fact]
    public void AddAddress_SettingDefault_ClearsPriorDefaultOfSameType()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);

        var first = profile.AddAddress(AddressType.Shipping, "1 First St", null, "Metropolis", null, "12345", "US", null, isDefault: true, Now, "user-1");
        var second = profile.AddAddress(AddressType.Shipping, "2 Second St", null, "Metropolis", null, "54321", "US", null, isDefault: true, Now, "user-1");

        Assert.False(profile.Addresses.Single(a => a.AddressId == first.AddressId).IsDefault);
        Assert.True(profile.Addresses.Single(a => a.AddressId == second.AddressId).IsDefault);
    }

    [Fact]
    public void AddAddress_DifferentTypes_DoNotShareDefaultInvariant()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);

        var shipping = profile.AddAddress(AddressType.Shipping, "1 First St", null, "Metropolis", null, "12345", "US", null, isDefault: true, Now, "user-1");
        var billing = profile.AddAddress(AddressType.Billing, "2 Second St", null, "Metropolis", null, "54321", "US", null, isDefault: true, Now, "user-1");

        Assert.True(profile.Addresses.Single(a => a.AddressId == shipping.AddressId).IsDefault);
        Assert.True(profile.Addresses.Single(a => a.AddressId == billing.AddressId).IsDefault);
    }

    [Fact]
    public void UpdateAddress_UnknownAddressId_ReturnsNotFound()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);

        var result = profile.UpdateAddress(Guid.NewGuid(), AddressType.Other, "1 First St", null, "Metropolis", null, "12345", "US", null, false, Now, "user-1");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public void RemoveAddress_UnknownAddressId_ReturnsNotFound()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);

        var result = profile.RemoveAddress(Guid.NewGuid(), Now, "user-1");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public void RemoveAddress_ExistingAddress_RemovesIt()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);
        var address = profile.AddAddress(AddressType.Other, "1 First St", null, "Metropolis", null, "12345", "US", null, false, Now, "user-1");

        var result = profile.RemoveAddress(address.AddressId, Now, "user-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Addresses);
    }

    [Fact]
    public void ReconcileContactCopy_NewerUpdate_IsApplied()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", "old@example.com", Now);

        var applied = profile.ReconcileContactCopy("new@example.com", "New Name", Now.AddMinutes(1), Now.AddMinutes(1));

        Assert.True(applied);
        Assert.Equal("new@example.com", profile.ContactCopy.Email);
        Assert.Equal("New Name", profile.ContactCopy.DisplayName);
    }

    [Fact]
    public void ReconcileContactCopy_StaleUpdate_IsIgnored()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", "old@example.com", Now);
        profile.ReconcileContactCopy("current@example.com", "Current Name", Now.AddMinutes(5), Now.AddMinutes(5));

        var applied = profile.ReconcileContactCopy("stale@example.com", "Stale Name", Now, Now.AddMinutes(6));

        Assert.False(applied);
        Assert.Equal("current@example.com", profile.ContactCopy.Email);
    }

    [Fact]
    public void Erase_FirstCall_TombstonesPiiAndReturnsTrue()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", "user@example.com", Now);
        profile.AddAddress(AddressType.Shipping, "1 First St", "Apt 2", "Metropolis", null, "12345", "US", "555-1234", true, Now, "user-1");

        var wasApplied = profile.Erase(Now.AddDays(1));

        Assert.True(wasApplied);
        Assert.Equal(ErasureStatus.Erased, profile.ErasureStatus);
        Assert.Equal("[erased]", profile.ContactCopy.Email);
        Assert.All(profile.Addresses, a => Assert.Null(a.Phone));
    }

    [Fact]
    public void Erase_SecondCall_IsIdempotentNoOp()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", "user@example.com", Now);
        profile.Erase(Now.AddDays(1));

        var wasAppliedAgain = profile.Erase(Now.AddDays(2));

        Assert.False(wasAppliedAgain);
    }

    [Fact]
    public void UpdatePreferences_TouchingNotificationOptIn_ReturnsTrue()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);
        var preferences = new Preferences("en-US", "USD", new NotificationOptIn(true, false, false), true);

        var touched = profile.UpdatePreferences(preferences, appInstalled: null, Now, "user-1");

        Assert.True(touched);
        Assert.Equal("en-US", profile.Preferences.Locale);
    }

    [Fact]
    public void UpdatePreferences_NoFieldsProvided_ReturnsFalse()
    {
        var profile = UserProfile.CreateFromRegistration("user-1", null, Now);

        var touched = profile.UpdatePreferences(null, appInstalled: null, Now, "user-1");

        Assert.False(touched);
    }
}
