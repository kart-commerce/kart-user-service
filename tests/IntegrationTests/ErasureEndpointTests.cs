using System.Net;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.IntegrationTests;

/// <summary>ADR-0016/ADR-0017: the internal, Admin-only erasure-intake endpoint.</summary>
public sealed class ErasureEndpointTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public ErasureEndpointTests(UserApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task SubmitErasureRequest_AsAdmin_Returns202Accepted()
    {
        var userId = await CreateProfileAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "admin-service-principal");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "admin");

        var response = await client.PostAsync($"/internal/v1/users/{userId}/erasure-requests", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task SubmitErasureRequest_Repeated_IsIdempotentNoOpReturning200()
    {
        var userId = await CreateProfileAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "admin-service-principal");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "admin");

        await client.PostAsync($"/internal/v1/users/{userId}/erasure-requests", content: null);
        var secondResponse = await client.PostAsync($"/internal/v1/users/{userId}/erasure-requests", content: null);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SubmitErasureRequest_WithoutAdminScope_Returns403()
    {
        var userId = await CreateProfileAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, userId);

        var response = await client.PostAsync($"/internal/v1/users/{userId}/erasure-requests", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitErasureRequest_UnknownUser_Returns404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "admin-service-principal");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "admin");

        var response = await client.PostAsync($"/internal/v1/users/{Guid.NewGuid()}/erasure-requests", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
