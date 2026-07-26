using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>
/// Declares this service's full RabbitMQ topology once at boot, ahead of the publisher/consumer
/// hosted services (the host runs <see cref="IHostedService.StartAsync"/> in registration
/// order). Logs and swallows a declare failure rather than crashing startup — every other
/// hosted service re-declares the same idempotent topology in its own reconnect loop anyway,
/// so a RabbitMQ outage at boot is not fatal, just delayed.
/// </summary>
public sealed class RabbitMqTopologyStartupHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    ILogger<RabbitMqTopologyStartupHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();
            RabbitMqTopologyProvisioner.Declare(channel, manifest);
            logger.LogInformation("Declared RabbitMQ topology for {Service} ({ExchangeCount} exchange(s), {QueueCount} queue(s)).",
                manifest.Service, manifest.Exchanges.Count, manifest.Queues.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not declare RabbitMQ topology at startup; publisher/consumer hosted services will retry this themselves once RabbitMQ is reachable.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
