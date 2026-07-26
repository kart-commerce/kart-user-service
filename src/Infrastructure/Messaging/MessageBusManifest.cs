namespace Kart.User.Infrastructure.Messaging;

/// <summary>
/// Strongly-typed mirror of <c>contracts/message-bus-manifest.json</c> — the config-driven
/// message bus (BRD §9): "Application startup reads a JSON manifest and declares all RabbitMQ
/// topology idempotently... no manual rabbitmqctl setup, no drift between environments."
/// </summary>
public sealed record MessageBusManifest(
    string Service,
    IReadOnlyList<ExchangeDefinition> Exchanges,
    IReadOnlyList<ExchangeDefinition> ExternalExchanges,
    IReadOnlyList<PublishedEventDefinition> PublishedEvents,
    IReadOnlyList<QueueDefinition> Queues,
    IReadOnlyList<DeadLetterQueueDefinition> DeadLetterQueues)
{
    public bool TryGetPublishedEvent(string eventType, out PublishedEventDefinition definition)
    {
        var found = PublishedEvents.FirstOrDefault(e => e.EventType == eventType);
        definition = found!;
        return found is not null;
    }

    public QueueDefinition GetQueue(string name) =>
        Queues.FirstOrDefault(q => q.Name == name)
            ?? throw new InvalidOperationException($"message-bus-manifest.json has no queue named '{name}'.");
}

public sealed record ExchangeDefinition(string Name, string Type, bool Durable);

public sealed record PublishedEventDefinition(string EventType, string Exchange, string RoutingKey);

public sealed record QueueBindingDefinition(string Exchange, string RoutingKey);

public sealed record DeadLetterDefinition(string Exchange, string RoutingKey);

public sealed record RetryTierDefinition(string Name, int TtlMs);

public sealed record RetryLadderDefinition(string RequeueTo, IReadOnlyList<RetryTierDefinition> Tiers);

public sealed record QueueDefinition(
    string Name,
    bool Durable,
    IReadOnlyList<QueueBindingDefinition> Bindings,
    DeadLetterDefinition? DeadLetter,
    RetryLadderDefinition? RetryLadder);

public sealed record DeadLetterQueueDefinition(string Name, string Exchange, string RoutingKey);
