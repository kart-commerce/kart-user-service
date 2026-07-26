using Kart.User.Application.Features.ReconcileIdentityContactCopy;
using Kart.User.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kart.User.UnitTests.Application;

public sealed class ReconcileIdentityContactCopyCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_OutOfOrderDelivery_CreatesShellProfile()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new ReconcileIdentityContactCopyCommandHandler(
            dbContext, new FixedDateTimeProvider(Now), NullLogger<ReconcileIdentityContactCopyCommandHandler>.Instance);

        await handler.Handle(new ReconcileIdentityContactCopyCommand("user-1", "user@example.com", "User One", Now), CancellationToken.None);

        var profile = await dbContext.UserProfiles.SingleAsync();
        Assert.Equal("user@example.com", profile.ContactCopy.Email);
        Assert.Equal("User One", profile.ContactCopy.DisplayName);
    }

    [Fact]
    public async Task Handle_DoesNotPublishUserProfileUpdated_OnlyInternalProjectionMarker()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new ReconcileIdentityContactCopyCommandHandler(
            dbContext, new FixedDateTimeProvider(Now), NullLogger<ReconcileIdentityContactCopyCommandHandler>.Instance);

        await handler.Handle(new ReconcileIdentityContactCopyCommand("user-1", "user@example.com", "User One", Now), CancellationToken.None);

        var outboxEvent = await dbContext.OutboxEvents.SingleAsync();
        Assert.Equal("UserReadModelProjectionRequested", outboxEvent.EventType);
    }

    [Fact]
    public async Task Handle_StaleUpdate_DoesNotWriteOutboxRow()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new ReconcileIdentityContactCopyCommandHandler(
            dbContext, new FixedDateTimeProvider(Now.AddMinutes(10)), NullLogger<ReconcileIdentityContactCopyCommandHandler>.Instance);

        await handler.Handle(new ReconcileIdentityContactCopyCommand("user-1", "first@example.com", "First", Now.AddMinutes(5)), CancellationToken.None);
        await handler.Handle(new ReconcileIdentityContactCopyCommand("user-1", "stale@example.com", "Stale", Now), CancellationToken.None);

        var profile = await dbContext.UserProfiles.SingleAsync();
        Assert.Equal("first@example.com", profile.ContactCopy.Email);
        Assert.Equal(1, await dbContext.OutboxEvents.CountAsync());
    }
}
