using System.Net;
using System.Net.Http.Json;
using Kart.User.Application.Common.Models;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.IntegrationTests;

public sealed class UpdatePreferencesEndpointTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public UpdatePreferencesEndpointTests(UserApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdatePreferences_Self_Succeeds()
    {
        var userId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateUserProfileOnRegistrationCommand(userId, "user@example.com"));
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);

        var response = await client.PatchAsJsonAsync($"/v1/users/{userId}", new
        {
            preferences = new
            {
                locale = "en-US",
                currency = "USD",
                notificationOptIn = new { email = true, sms = false, push = true },
                marketingConsent = true
            },
            appInstalled = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(updated);
        Assert.Equal("en-US", updated.Preferences.Locale);
        Assert.True(updated.AppInstalled);
    }

    [Fact]
    public async Task UpdatePreferences_UnknownUser_Returns404()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);

        var response = await client.PatchAsJsonAsync($"/v1/users/{userId}", new { appInstalled = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
