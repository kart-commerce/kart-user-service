using System.Security.Cryptography;
using System.Text;
using Kart.Shared.Domain;

namespace Kart.User.Domain.Entities;

/// <summary>
/// Standard platform Outbox row (BRD §11; database-design.md's <c>user_outbox_events</c>) —
/// inherits <see cref="OutboxEventBase"/> (Kart.Shared.Domain) rather than re-declaring
/// Id/AggregateId/EventType/Payload/OccurredAt/PublishedAt locally, per kart-user-service being
/// the first Kart service to actually consume the shared package.
///
/// Carries two independent completion markers because this table serves two independent
/// consumers of the same row: <see cref="PublishedAt"/> (inherited) is set once
/// <c>Infrastructure/Messaging/OutboxRelayHostedService</c> has relayed the row onto RabbitMQ for
/// this service's external event-contract.md-documented events; <see cref="ProjectedAt"/> is set
/// once <c>Infrastructure/Messaging/ReadModelProjectionHostedService</c> has folded the row into
/// the <c>user_read_model</c> MongoDB projection. A row whose <see cref="EventType"/> is not one
/// of the three externally-published event types (i.e. the internal
/// <c>UserReadModelProjectionRequested</c> marker used for registration-creation and
/// contactCopy-only reconciliation, which ddd-model.md's Modeling Decision explicitly forbids
/// re-publishing as <c>UserProfileUpdated</c>) is still eligible for projection, just never
/// externally relayed.
/// </summary>
public sealed class OutboxEvent : OutboxEventBase
{
    /// <summary>Internal-only marker event type: never externally published (not part of
    /// event-contract.md's Published Events table), but still projected into the read model.
    /// Used for registration-creation and contactCopy-only reconciliation, per ddd-model.md's
    /// rule against re-publishing <c>UserProfileUpdated</c> for the latter.</summary>
    public const string ReadModelProjectionRequested = "UserReadModelProjectionRequested";

    public string UserId { get; private set; } = string.Empty;
    public DateTimeOffset? ProjectedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "system:user-outbox-poller";

    private OutboxEvent() { }

    public static OutboxEvent Create(string userId, string eventType, string payloadJson, DateTimeOffset now, string createdBy)
    {
        return new OutboxEvent(
            id: Guid.NewGuid(),
            aggregateId: DeterministicGuidFrom(userId),
            eventType: eventType,
            payload: payloadJson,
            occurredAt: now)
        {
            UserId = userId,
            CreatedBy = createdBy,
            UpdatedAt = now
        };
    }

    private OutboxEvent(Guid id, Guid aggregateId, string eventType, string payload, DateTimeOffset occurredAt)
        : base(id, aggregateId, eventType, payload, occurredAt)
    {
    }

    /// <summary>
    /// <see cref="OutboxEventBase.AggregateId"/> is typed <c>Guid</c>, but database-design.md
    /// declares <c>user_profiles.user_id</c> as <c>TEXT</c>, not <c>UUID</c> — this service's
    /// own querying always goes through <see cref="UserId"/> (the real key), never
    /// <c>AggregateId</c>, so a stable, deterministic derivation (not a real parse) is all
    /// <c>AggregateId</c> needs to be: reproducible for the same <paramref name="userId"/>,
    /// never a source of a <see cref="FormatException"/> if Identity's id format ever isn't
    /// GUID-shaped.
    /// </summary>
    private static Guid DeterministicGuidFrom(string userId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes(userId)));

    public void MarkProjected(DateTimeOffset projectedAt)
    {
        ProjectedAt = projectedAt;
        UpdatedAt = projectedAt;
        UpdatedBy = "system:user-read-model-projector";
    }

    public new void MarkPublished(DateTimeOffset publishedAt)
    {
        base.MarkPublished(publishedAt);
        UpdatedAt = publishedAt;
        UpdatedBy = "system:user-outbox-poller";
    }
}
