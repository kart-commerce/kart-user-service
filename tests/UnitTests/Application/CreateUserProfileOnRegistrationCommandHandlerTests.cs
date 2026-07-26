using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using Kart.User.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kart.User.UnitTests.Application;

public sealed class CreateUserProfileOnRegistrationCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_NewUser_CreatesProfileAndProjectionOutboxRow()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new CreateUserProfileOnRegistrationCommandHandler(
            dbContext, new FixedDateTimeProvider(Now), NullLogger<CreateUserProfileOnRegistrationCommandHandler>.Instance);

        await handler.Handle(new CreateUserProfileOnRegistrationCommand("user-1", "user@example.com"), CancellationToken.None);

        var profile = await dbContext.UserProfiles.SingleAsync();
        Assert.Equal("user-1", profile.UserId);
        Assert.Equal("user@example.com", profile.ContactCopy.Email);

        var outboxEvent = await dbContext.OutboxEvents.SingleAsync();
        Assert.Equal("UserReadModelProjectionRequested", outboxEvent.EventType);
        Assert.Null(outboxEvent.PublishedAt);
        Assert.Null(outboxEvent.ProjectedAt);
    }

    [Fact]
    public async Task Handle_DuplicateDelivery_IsIdempotentNoOp()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new CreateUserProfileOnRegistrationCommandHandler(
            dbContext, new FixedDateTimeProvider(Now), NullLogger<CreateUserProfileOnRegistrationCommandHandler>.Instance);

        await handler.Handle(new CreateUserProfileOnRegistrationCommand("user-1", "user@example.com"), CancellationToken.None);
        await handler.Handle(new CreateUserProfileOnRegistrationCommand("user-1", "user@example.com"), CancellationToken.None);

        Assert.Equal(1, await dbContext.UserProfiles.CountAsync());
        Assert.Equal(1, await dbContext.OutboxEvents.CountAsync());
    }
}
