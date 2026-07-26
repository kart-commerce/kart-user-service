using Kart.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.User.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md's <c>user_outbox_events</c> table.</summary>
public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("user_outbox_events", t => t.HasCheckConstraint(
            "ck_user_outbox_events_event_type",
            $"event_type IN ('UserProfileUpdated', 'UserNotificationPreferenceUpdated', 'UserDataErased', '{OutboxEvent.ReadModelProjectionRequested}')"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.ProjectedAt).HasColumnName("projected_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // Two independent partial indexes over the same single column — each needs its own
        // distinguishing index *name* passed directly to HasIndex (not just HasDatabaseName),
        // since EF Core otherwise treats two HasIndex(e => e.Id) calls as configuring the same
        // index (by property list) and silently drops the first.

        // The Outbox relay's own unpublished-row scan (Infrastructure/Messaging/OutboxRelayHostedService).
        builder.HasIndex(e => e.Id, "idx_user_outbox_unpublished").HasFilter("published_at IS NULL");

        // The read-model projector's own unprojected-row scan (Infrastructure/Messaging/ReadModelProjectionHostedService).
        builder.HasIndex(e => e.Id, "idx_user_outbox_unprojected").HasFilter("projected_at IS NULL");
    }
}
