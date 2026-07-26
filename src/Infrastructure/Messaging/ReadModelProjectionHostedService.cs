using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Mapping;
using Kart.User.Infrastructure.Persistence;
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
        foreach (var userId in pending.Select(e => e.UserId).Distinct())
        {
            var profile = await dbContext.UserProfiles
                .Include(p => p.Addresses)
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (profile is null)
            {
                logger.LogWarning("Outbox row references user {UserId} with no corresponding user_profiles row; skipping projection.", userId);
                continue;
            }

            await readModel.UpsertAsync(UserProfileMapper.ToResponse(profile), cancellationToken);
        }

        foreach (var outboxEvent in pending)
        {
            outboxEvent.MarkProjected(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
