using Kart.User.Application.Features.AddAddress;
using Kart.User.Domain.Entities;
using Kart.User.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kart.User.UnitTests.Application;

public sealed class AddAddressCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ExistingProfile_AddsAddressAndPublishesUserProfileUpdated()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        dbContext.UserProfiles.Add(Kart.User.Domain.Entities.UserProfile.CreateFromRegistration("user-1", null, Now));
        await dbContext.SaveChangesAsync();

        var handler = new AddAddressCommandHandler(dbContext, new FixedDateTimeProvider(Now), NullLogger<AddAddressCommandHandler>.Instance);
        var command = new AddAddressCommand("user-1", "user-1", "Shipping", "1 First St", null, "Metropolis", "NY", "12345", "US", null, true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDefault);
        var outboxEvent = await dbContext.OutboxEvents.SingleAsync();
        Assert.Equal("UserProfileUpdated", outboxEvent.EventType);
    }

    [Fact]
    public async Task Handle_UnknownUser_ReturnsNotFound()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new AddAddressCommandHandler(dbContext, new FixedDateTimeProvider(Now), NullLogger<AddAddressCommandHandler>.Instance);
        var command = new AddAddressCommand("missing-user", "missing-user", "Shipping", "1 First St", null, "Metropolis", "NY", "12345", "US", null, false);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
