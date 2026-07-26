using Kart.Shared.Auditing;
using Kart.User.Application.Features.ProcessErasureRequest;
using Kart.User.Domain.Entities;
using Kart.User.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Kart.User.UnitTests.Application;

public sealed class ProcessErasureRequestCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ActiveProfile_TombstonesAndPublishesUserDataErasedAndWritesAudit()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        dbContext.UserProfiles.Add(Kart.User.Domain.Entities.UserProfile.CreateFromRegistration("user-1", "user@example.com", Now));
        await dbContext.SaveChangesAsync();

        var auditLogWriter = Substitute.For<IAuditLogWriter>();
        var handler = new ProcessErasureRequestCommandHandler(dbContext, new FixedDateTimeProvider(Now.AddDays(1)), auditLogWriter);

        var result = await handler.Handle(new ProcessErasureRequestCommand("user-1", "admin-service"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasAlreadyErased);

        var outboxEvent = await dbContext.OutboxEvents.SingleAsync();
        Assert.Equal("UserDataErased", outboxEvent.EventType);

        await auditLogWriter.Received(1).WriteAsync(Arg.Is<AuditLogEntry>(e => e.EntityId == "user-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyErasedProfile_IsIdempotentNoOp()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var profile = Kart.User.Domain.Entities.UserProfile.CreateFromRegistration("user-1", "user@example.com", Now);
        profile.Erase(Now.AddDays(1));
        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var auditLogWriter = Substitute.For<IAuditLogWriter>();
        var handler = new ProcessErasureRequestCommandHandler(dbContext, new FixedDateTimeProvider(Now.AddDays(2)), auditLogWriter);

        var result = await handler.Handle(new ProcessErasureRequestCommand("user-1", "admin-service"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.WasAlreadyErased);
        Assert.Empty(await dbContext.OutboxEvents.ToListAsync());
        await auditLogWriter.DidNotReceive().WriteAsync(Arg.Any<AuditLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownUser_ReturnsNotFound()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var auditLogWriter = Substitute.For<IAuditLogWriter>();
        var handler = new ProcessErasureRequestCommandHandler(dbContext, new FixedDateTimeProvider(Now), auditLogWriter);

        var result = await handler.Handle(new ProcessErasureRequestCommand("missing-user", "admin-service"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
