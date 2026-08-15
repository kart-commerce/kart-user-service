using System.Text.Json;
using System.Text.Json.Serialization;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;
using Kart.User.Application.Common;
using Kart.User.Application.Features.CreateUserProfileOnRegistration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>Consumes Identity's <c>UserRegistered</c> (<c>identity.exchange</c> /
/// <c>identity.user.registered</c>) — USR-1's trigger.</summary>
public sealed class UserRegisteredConsumerHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    IServiceScopeFactory scopeFactory,
    ILogger<UserRegisteredConsumerHostedService> logger)
    : RabbitMqConsumerHostedServiceBase(connectionFactory, manifest, scopeFactory, logger, "x-user-service-retry-count")
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override string QueueName => "user.user-registered.queue";

    protected override async Task ProcessAsync(ReadOnlyMemory<byte> body, IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);

        var payload = JsonSerializer.Deserialize<UserRegisteredPayload>(body.Span, SerializerOptions)
            ?? throw new InvalidOperationException("UserRegistered payload deserialized to null.");

        logger.LogInformation(
            "Stage {Stage}: UserRegistered event {EventId} consumed from {Queue} for user {UserId}",
            "UserRegisteredConsumed",
            payload.EventId,
            QueueName,
            payload.UserId);

        var sender = scopedProvider.GetRequiredService<ISender>();
        logger.LogInformation(
            "Stage {Stage}: dispatching CreateUserProfileOnRegistrationCommand for user {UserId}",
            "CreateUserProfileOnRegistrationCommandDispatched",
            payload.UserId);
        await sender.Send(new CreateUserProfileOnRegistrationCommand(payload.UserId, payload.Email), cancellationToken);
    }

    private sealed record UserRegisteredPayload(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("eventId")] string? EventId);
}
