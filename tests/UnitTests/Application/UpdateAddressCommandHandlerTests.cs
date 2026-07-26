using Kart.User.Application.Features.UpdateAddress;
using Kart.User.Domain.Enums;
using Kart.User.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.UnitTests.Application;

public sealed class UpdateAddressCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ExistingAddress_ReplacesItWholeRecord()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var profile = Kart.User.Domain.Entities.UserProfile.CreateFromRegistration("user-1", null, Now);
        var address = profile.AddAddress(AddressType.Shipping, "1 First St", null, "Metropolis", "NY", "12345", "US", null, false, Now, "user-1");
        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateAddressCommandHandler(dbContext, new FixedDateTimeProvider(Now));
        var command = new UpdateAddressCommand(
            "user-1", address.AddressId, "user-1", "Billing", "2 Second St", "Apt 4", "Gotham", "NY", "54321", "US", "555-1234", true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Billing", result.Value.Type);
        Assert.True(result.Value.IsDefault);
        var outboxEvent = await dbContext.OutboxEvents.SingleAsync();
        Assert.Equal("UserProfileUpdated", outboxEvent.EventType);
    }

    [Fact]
    public async Task Handle_UnknownAddress_ReturnsNotFound()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        dbContext.UserProfiles.Add(Kart.User.Domain.Entities.UserProfile.CreateFromRegistration("user-1", null, Now));
        await dbContext.SaveChangesAsync();

        var handler = new UpdateAddressCommandHandler(dbContext, new FixedDateTimeProvider(Now));
        var command = new UpdateAddressCommand("user-1", Guid.NewGuid(), "user-1", "Billing", "2 Second St", null, "Gotham", "NY", "54321", "US", null, false);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
