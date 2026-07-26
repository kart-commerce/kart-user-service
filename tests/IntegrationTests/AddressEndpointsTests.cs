using System.Net;
using System.Net.Http.Json;
using Kart.User.Application.Common.Models;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.IntegrationTests;

public sealed class AddressEndpointsTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public AddressEndpointsTests(UserApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> CreateProfileAsync()
    {
        var userId = Guid.NewGuid().ToString();
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CreateUserProfileOnRegistrationCommand(userId, "user@example.com"));
        return userId;
    }

    private HttpClient CreateAuthenticatedClient(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);
        return client;
    }

    [Fact]
    public async Task AddAddress_ThenUpdateThenRemove_FullLifecycle()
    {
        var userId = await CreateProfileAsync();
        var client = CreateAuthenticatedClient(userId);

        var addResponse = await client.PostAsJsonAsync($"/v1/users/{userId}/addresses", new
        {
            type = "Shipping",
            line1 = "1 First St",
            city = "Metropolis",
            region = "NY",
            postalCode = "12345",
            countryCode = "US",
            isDefault = true
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = await addResponse.Content.ReadFromJsonAsync<AddressResponse>();
        Assert.NotNull(added);
        Assert.True(added.IsDefault);

        var updateResponse = await client.PatchAsJsonAsync($"/v1/users/{userId}/addresses/{added.AddressId}", new
        {
            type = "Billing",
            line1 = "2 Second St",
            city = "Gotham",
            region = "NY",
            postalCode = "54321",
            countryCode = "US",
            isDefault = true
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<AddressResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Billing", updated.Type);

        var removeResponse = await client.DeleteAsync($"/v1/users/{userId}/addresses/{added.AddressId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
    }

    [Fact]
    public async Task AddAddress_InvalidPostalCode_Returns400()
    {
        var userId = await CreateProfileAsync();
        var client = CreateAuthenticatedClient(userId);

        var response = await client.PostAsJsonAsync($"/v1/users/{userId}/addresses", new
        {
            type = "Shipping",
            line1 = "1 First St",
            city = "Metropolis",
            postalCode = "not-a-zip",
            countryCode = "US"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddAddress_ActingAsDifferentUser_Returns403()
    {
        var userId = await CreateProfileAsync();
        var client = CreateAuthenticatedClient("someone-else");

        var response = await client.PostAsJsonAsync($"/v1/users/{userId}/addresses", new
        {
            type = "Shipping",
            line1 = "1 First St",
            city = "Metropolis",
            postalCode = "12345",
            countryCode = "US"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddAddress_Unauthenticated_Returns401()
    {
        var userId = await CreateProfileAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/v1/users/{userId}/addresses", new
        {
            type = "Shipping",
            line1 = "1 First St",
            city = "Metropolis",
            postalCode = "12345",
            countryCode = "US"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveAddress_UnknownAddressId_Returns404()
    {
        var userId = await CreateProfileAsync();
        var client = CreateAuthenticatedClient(userId);

        var response = await client.DeleteAsync($"/v1/users/{userId}/addresses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
