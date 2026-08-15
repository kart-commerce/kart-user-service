using System.Text;
using Kart.Shared.Messaging;
using Kart.User.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>
/// The Outbox poller/relay (BRD §11): reads unpublished <c>user_outbox_events</c> rows and
/// publishes them to RabbitMQ, marking <c>published_at</c> only after the broker accepts them.
/// Rows whose <see cref="Domain.Entities.OutboxEvent.EventType"/> is not one of this service's
/// three externally-documented published events (i.e. the internal
/// <see cref="Domain.Entities.OutboxEvent.ReadModelProjectionRequested"/> marker) are marked
/// published without an actual AMQP publish — there is nothing external to relay for them; see
/// <see cref="ReadModelProjectionHostedService"/> for the independent poller that does act on
/// every row regardless of type.
/// </summary>
public sealed class OutboxRelayHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxRelayHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologyProvisioner.Declare(channel, manifest);
                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "User outbox relay lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunRelayLoopAsync(IModel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingBatchAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayPendingBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();

        // Ordered client-side, not via ORDER BY: the unpublished set is always small (the
        // whole point of the Outbox pattern is a continuously-drained queue, not an
        // ever-growing table scan) and some providers (SQLite, used in this repo's own
        // integration tests) cannot translate ORDER BY over DateTimeOffset server-side.
        var pending = (await dbContext.OutboxEvents
                .Where(e => e.PublishedAt == null)
                .ToListAsync(cancellationToken))
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var outboxEvent in pending)
        {
            if (!manifest.TryGetPublishedEvent(outboxEvent.EventType, out var published))
            {
                // Internal-only marker (e.g. ReadModelProjectionRequested) — nothing to relay.
                outboxEvent.MarkPublished(now);
                continue;
            }

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.Id.ToString();
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: published.Exchange,
                routingKey: published.RoutingKey,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
