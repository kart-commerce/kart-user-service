using System.Net;
using System.Net.Http.Json;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using Kart.User.IntegrationTests;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.ContractTests;

public sealed class AddAddressContractTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public AddAddressContractTests(UserApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Contract_DefinesAddAddress_With201And400And404()
    {
        var operation = ApiContractDocument.Paths.Map("/v1/users/{userId}/addresses").Map("post");

        Assert.Equal("addAddress", operation.Str("operationId"));
        var responses = operation.Map("responses");
        Assert.True(responses.HasKey("201"));
        Assert.True(responses.HasKey("400"));
        Assert.True(responses.HasKey("404"));
    }

    [Fact]
    public async Task LiveBehavior_SelfScopedCaller_Returns201WithAddressId()
    {
        var userId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateUserProfileOnRegistrationCommand(userId, "user@example.com"));
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);

        var response = await client.PostAsJsonAsync($"/v1/users/{userId}/addresses", new
        {
            type = "Shipping",
            line1 = "1 First St",
            city = "Metropolis",
            postalCode = "12345",
            countryCode = "US"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("addressId"));
    }
}
