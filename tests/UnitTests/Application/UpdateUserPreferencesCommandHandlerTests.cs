using Kart.User.Application.Features.UpdateUserPreferences;
using Kart.User.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.UnitTests.Application;

public sealed class UpdateUserPreferencesCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_TouchingNotificationOptIn_PublishesBothEvents()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        dbContext.UserProfiles.Add(Kart.User.Domain.Entities.UserProfile.CreateFromRegistration("user-1", null, Now));
        await dbContext.SaveChangesAsync();

        var handler = new UpdateUserPreferencesCommandHandler(dbContext, new FixedDateTimeProvider(Now));
        var preferences = new PreferencesInput("en-US", "USD", new NotificationOptInInput(true, false, true), true);
        var command = new UpdateUserPreferencesCommand("user-1", "user-1", preferences, AppInstalled: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("en-US", result.Value.Preferences.Locale);
        var eventTypes = (await dbContext.OutboxEvents.ToListAsync()).Select(e => e.EventType).ToList();
        Assert.Contains("UserProfileUpdated", eventTypes);
        Assert.Contains("UserNotificationPreferenceUpdated", eventTypes);
        Assert.Equal(2, eventTypes.Count);
    }

    [Fact]
    public async Task Handle_UnknownUser_ReturnsNotFound()
    {
        await using var dbContext = InMemoryUserDbContextFactory.Create();
        var handler = new UpdateUserPreferencesCommandHandler(dbContext, new FixedDateTimeProvider(Now));

        var result = await handler.Handle(new UpdateUserPreferencesCommand("missing-user", "missing-user", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
