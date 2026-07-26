using RabbitMQ.Client;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>
/// Declares the full topology idempotently — exchanges, then DLQs, then each queue's retry-tier
/// TTL "parking lot" queues, then the queue itself and its bindings (BRD §8.1/§9). Every
/// <c>QueueDeclare</c>/<c>ExchangeDeclare</c> call is safe to run repeatedly (RabbitMQ no-ops on
/// an identical redeclare), so this runs at startup and again inside every hosted service's own
/// reconnect loop.
/// </summary>
public static class RabbitMqTopologyProvisioner
{
    public static void Declare(IModel channel, MessageBusManifest manifest)
    {
        foreach (var exchange in manifest.Exchanges.Concat(manifest.ExternalExchanges))
        {
            channel.ExchangeDeclare(exchange.Name, exchange.Type, durable: exchange.Durable);
        }

        foreach (var dlq in manifest.DeadLetterQueues)
        {
            channel.QueueDeclare(dlq.Name, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(dlq.Name, dlq.Exchange, dlq.RoutingKey);
        }

        foreach (var queue in manifest.Queues)
        {
            DeclareRetryLadder(channel, queue.RetryLadder);

            IDictionary<string, object>? arguments = queue.DeadLetter is null
                ? null
                : new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = queue.DeadLetter.Exchange,
                    ["x-dead-letter-routing-key"] = queue.DeadLetter.RoutingKey,
                };

            channel.QueueDeclare(queue.Name, durable: queue.Durable, exclusive: false, autoDelete: false, arguments: arguments);

            foreach (var binding in queue.Bindings)
            {
                channel.QueueBind(queue.Name, binding.Exchange, binding.RoutingKey);
            }
        }
    }

    /// <summary>
    /// TTL-ladder retry ("parking lot" queues, BRD §8.1): each tier is its own TTL-only queue with
    /// no consumer; when a message's TTL expires, RabbitMQ dead-letters it back to
    /// <see cref="RetryLadderDefinition.RequeueTo"/> (the original queue) — no external scheduler
    /// needed.
    /// </summary>
    private static void DeclareRetryLadder(IModel channel, RetryLadderDefinition? retryLadder)
    {
        if (retryLadder is null)
        {
            return;
        }

        foreach (var tier in retryLadder.Tiers)
        {
            var arguments = new Dictionary<string, object>
            {
                ["x-message-ttl"] = tier.TtlMs,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = retryLadder.RequeueTo,
            };

            channel.QueueDeclare(tier.Name, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }
    }
}
