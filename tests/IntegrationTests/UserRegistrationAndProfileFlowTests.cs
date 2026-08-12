using System.Net;
using System.Net.Http.Json;
using Kart.User.Application.Common.Models;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using Kart.User.IntegrationTests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.IntegrationTests;

/// <summary>
/// USR-1 + USR-2 end to end: since no real RabbitMQ broker runs in this test environment (see
/// UserApiFactory), the "UserRegistered consumed" step is simulated by dispatching the same
/// MediatR command a real <c>UserRegisteredConsumerHostedService</c> delivery would dispatch —
/// this asserts the write-side + projection-poller + read-endpoint pipeline exactly as
/// production wires it, just without the AMQP hop itself.
/// </summary>
public sealed class UserRegistrationAndProfileFlowTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public UserRegistrationAndProfileFlowTests(UserApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Registration_EventuallyProjectsIntoReadModel_AndIsServedByGetEndpoint()
    {
        var userId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateUserProfileOnRegistrationCommand(userId, "user@example.com"));
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);

        var profile = await Eventually.Assert(
            async () =>
            {
                var response = await client.GetAsync($"/v1/users/{userId}");
                return response.StatusCode == HttpStatusCode.OK
                    ? await response.Content.ReadFromJsonAsync<UserProfileResponse>()
                    : null;
            },
            p => p.UserId == userId);

        Assert.Equal("user@example.com", profile.Email);
        Assert.Empty(profile.Addresses);
    }

    [Fact]
    public async Task GetUserProfile_UnknownUser_Returns404()
    {
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);

        var response = await client.GetAsync($"/v1/users/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
