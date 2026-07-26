using System.Net;
using System.Net.Http.Json;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using Kart.User.IntegrationTests;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.ContractTests;

public sealed class GetUserProfileContractTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public GetUserProfileContractTests(UserApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Contract_DefinesGetUserProfile_With200And404()
    {
        var operation = ApiContractDocument.Paths.Map("/v1/users/{userId}").Map("get");

        Assert.Equal("getUserProfile", operation.Str("operationId"));
        var responses = operation.Map("responses");
        Assert.True(responses.HasKey("200"));
        Assert.True(responses.HasKey("404"));
    }

    [Fact]
    public async Task LiveBehavior_ExistingUser_Returns200WithRequiredFields()
    {
        var userId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateUserProfileOnRegistrationCommand(userId, "user@example.com"));
        }

        var client = _factory.CreateClient();
        HttpResponseMessage response;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        do
        {
            response = await client.GetAsync($"/v1/users/{userId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                break;
            }

            await Task.Delay(200);
        } while (DateTime.UtcNow < deadline);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);

        // api-contract.yaml UserProfileResponse: required [userId, addresses, preferences]
        Assert.True(body.ContainsKey("userId"));
        Assert.True(body.ContainsKey("addresses"));
        Assert.True(body.ContainsKey("preferences"));
    }

    [Fact]
    public async Task LiveBehavior_UnknownUser_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/v1/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
