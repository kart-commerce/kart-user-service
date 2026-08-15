using System.Diagnostics;
using Kart.User.Application.Common;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Mapping;
using Kart.User.Infrastructure.Persistence;
using Kart.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kart.User.Infrastructure.Messaging;

/// <summary>
/// The CQRS read-side projector: an in-process poller (no RabbitMQ) reading unprojected
/// <c>user_outbox_events</c> rows and rebuilding the corresponding <c>user_read_model</c>
/// MongoDB document from the current PostgreSQL write-model state — never from the outbox
/// row's own payload, so the read model "must be rebuildable from the PostgreSQL write model"
/// (requirement-spec.md §4) is literally true, not just true-by-construction of the payload.
///
/// Acts on every outbox row regardless of <c>EventType</c>, including the internal
/// <see cref="Domain.Entities.OutboxEvent.ReadModelProjectionRequested"/> marker used for
/// registration-creation and contactCopy-only reconciliation (ddd-model.md's rule against
/// re-publishing <c>UserProfileUpdated</c> for the latter) — this is precisely why projection
/// tracks its own independent <c>projected_at</c> completion marker rather than reusing
/// <c>published_at</c>, which only reflects the three externally-published event types
/// (see <see cref="OutboxRelayHostedService"/>).
/// </summary>
public sealed class ReadModelProjectionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReadModelProjectionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    /// <summary>
    /// This poller has no RabbitMQ span of its own to inherit from (it runs seconds after the
    /// original request, on an unrelated async context), so it starts its own span parented off
    /// each outbox row's stored <see cref="OutboxEvent.TraceParent"/> instead.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new("Kart.User.ReadModelProjection", "1.0.0");

    /// <summary>Maps an outbox row's <c>CreatedBy</c> to the Flow that produced it. An unrecognized
    /// value deliberately gets no Flow tag rather than a guessed one — extend as new flows write
    /// through this projector.</summary>
    private static string? FlowFor(string createdBy) => createdBy switch
    {
        "system:identity-registration-consumer" => FlowNames.UserRegistrationLoginAuthentication,
        _ => null,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProjectPendingBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "User read-model projection cycle failed; will retry next poll.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProjectPendingBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var readModel = scope.ServiceProvider.GetRequiredService<IUserReadModelRepository>();

        // Ordered client-side — see OutboxRelayHostedService's identical comment: the
        // unprojected set is always small, and SQLite (this repo's own integration tests)
        // cannot translate ORDER BY over DateTimeOffset server-side.
        var pending = (await dbContext.OutboxEvents
                .Where(e => e.ProjectedAt == null)
                .ToListAsync(cancellationToken))
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var group in pending.GroupBy(e => e.UserId))
        {
            var userId = group.Key;

            // Several pending rows can belong to the same user in one poll cycle (e.g. a
            // registration followed immediately by a preference update) — the oldest one drives
            // this write's Flow/trace attribution, since it's the row that's waited longest and
            // is most likely the one an operator is actually chasing.
            var drivingEvent = group.OrderBy(e => e.OccurredAt).First();
            var flow = FlowFor(drivingEvent.CreatedBy);

            using var flowScope = flow is not null ? KartFlowContext.Push(flow) : null;
            using var activity = drivingEvent.TraceParent is not null
                ? ActivitySource.StartActivity("read-model projection", ActivityKind.Internal, drivingEvent.TraceParent)
                : ActivitySource.StartActivity("read-model projection", ActivityKind.Internal);

            var profile = await dbContext.UserProfiles
                .Include(p => p.Addresses)
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (profile is null)
            {
                logger.LogWarning("Outbox row references user {UserId} with no corresponding user_profiles row; skipping projection.", userId);
                continue;
            }

            await readModel.UpsertAsync(UserProfileMapper.ToResponse(profile), cancellationToken);

            logger.LogInformation(
                "Stage {Stage}: read model write persisted for user {UserId} from outbox event {OutboxEventId}",
                "ReadModelWritePersisted",
                userId,
                drivingEvent.Id);
        }

        foreach (var outboxEvent in pending)
        {
            outboxEvent.MarkProjected(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
