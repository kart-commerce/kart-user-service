using Kart.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.User.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md's <c>user_profiles</c> table.</summary>
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).HasColumnName("user_id");

        builder.OwnsOne(p => p.ContactCopy, contact =>
        {
            contact.Property(c => c.Email).HasColumnName("email_copy");
            contact.Property(c => c.DisplayName).HasColumnName("display_name_copy");
            contact.Property(c => c.UpdatedAt).HasColumnName("contact_copy_updated_at");
        });

        // One JSONB column for the whole Preferences value object — requirement-spec.md §2:
        // "a small fixed initial set... stored as a single JSONB column on the write model...
        // so additive fields don't require a schema migration." (EF Core 8 cannot mix a
        // column-mapped owned type with a nested JSON-mapped owned type within it, which
        // database-design.md's separate notification_opt_in-only-JSONB sketch would require —
        // this reconciles that by favoring requirement-spec.md's simpler single-JSONB-column
        // description, still fully additive-friendly.)
        builder.OwnsOne(p => p.Preferences, preferences =>
        {
            preferences.ToJson("preferences");
            preferences.Property(p => p.Locale).HasJsonPropertyName("locale");
            preferences.Property(p => p.Currency).HasJsonPropertyName("currency");
            preferences.Property(p => p.MarketingConsent).HasJsonPropertyName("marketingConsent");

            preferences.OwnsOne(p => p.NotificationOptIn, optIn =>
            {
                optIn.Property(o => o.Email).HasJsonPropertyName("email");
                optIn.Property(o => o.Sms).HasJsonPropertyName("sms");
                optIn.Property(o => o.Push).HasJsonPropertyName("push");
            });
        });

        builder.Property(p => p.AppInstalled).HasColumnName("app_installed").HasDefaultValue(false);

        builder.Property(p => p.ErasureStatus)
            .HasColumnName("erasure_status")
            .HasConversion(EnumDbValueConverters.ErasureStatusConverter)
            .HasDefaultValue(Domain.Enums.ErasureStatus.Active);
        builder.Property(p => p.ErasedAt).HasColumnName("erased_at");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(p => p.Addresses)
            .WithOne()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(UserProfile.Addresses))!
            .SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}
