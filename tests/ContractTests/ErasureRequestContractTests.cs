using System.Net;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using Kart.User.IntegrationTests;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.User.ContractTests;

/// <summary>ADR-0016/ADR-0017: the internal, Admin-only erasure-intake endpoint.</summary>
public sealed class ErasureRequestContractTests : IClassFixture<UserApiFactory>, IAsyncLifetime
{
    private readonly UserApiFactory _factory;

    public ErasureRequestContractTests(UserApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Contract_DefinesErasureRequestEndpoint_AsInternalAdminScoped()
    {
        var operation = ApiContractDocument.Paths
            .Map("/internal/v1/users/{userId}/erasure-requests")
            .Map("post");

        Assert.Equal("submitErasureRequest", operation.Str("operationId"));

        var securitySchemes = ApiContractDocument.Components.Map("securitySchemes").Map("clientCredentials");
        var scopes = securitySchemes.Map("flows").Map("clientCredentials").Map("scopes");
        Assert.True(scopes.HasKey("admin"));
        Assert.True(scopes.HasKey("self"));

        var responses = operation.Map("responses");
        Assert.True(responses.HasKey("202"));
        Assert.True(responses.HasKey("403"));
        Assert.True(responses.HasKey("404"));
    }

    [Fact]
    public async Task LiveBehavior_AdminScopedCaller_Returns202()
    {
        var userId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateUserProfileOnRegistrationCommand(userId, "user@example.com"));
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "admin-service-principal");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "admin");

        var response = await client.PostAsync($"/internal/v1/users/{userId}/erasure-requests", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task LiveBehavior_CallerWithoutAdminScope_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "some-user");

        var response = await client.PostAsync($"/internal/v1/users/{Guid.NewGuid()}/erasure-requests", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
