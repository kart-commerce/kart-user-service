using System.Text.Json;
using System.Text.Json.Serialization;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;
using Kart.User.Application.Common;
using Kart.User.Application.Features.ReconcileIdentityContactCopy;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>Consumes Identity's <c>UserAccountUpdated</c> (<c>identity.exchange</c> /
/// <c>identity.user-account.updated</c>) — USR-7's trigger (ADR-0006).</summary>
public sealed class UserAccountUpdatedConsumerHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    IServiceScopeFactory scopeFactory,
    ILogger<UserAccountUpdatedConsumerHostedService> logger)
    : RabbitMqConsumerHostedServiceBase(connectionFactory, manifest, scopeFactory, logger, "x-user-service-retry-count")
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override string QueueName => "user.user-account-updated.queue";

    protected override async Task ProcessAsync(ReadOnlyMemory<byte> body, IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);
        logger.LogInformation("Stage {Stage}: UserAccountUpdated consumed from {Queue}", "UserAccountUpdatedConsumed", QueueName);

        var payload = JsonSerializer.Deserialize<UserAccountUpdatedPayload>(body.Span, SerializerOptions)
            ?? throw new InvalidOperationException("UserAccountUpdated payload deserialized to null.");

        var sender = scopedProvider.GetRequiredService<ISender>();
        await sender.Send(
            new ReconcileIdentityContactCopyCommand(payload.UserId, payload.Email, payload.DisplayName, payload.UpdatedAt),
            cancellationToken);
    }

    private sealed record UserAccountUpdatedPayload(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);
}
