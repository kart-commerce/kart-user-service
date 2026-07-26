using Kart.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.User.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md's <c>addresses</c> table — child entity of <c>UserProfile</c>.</summary>
public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasKey(a => a.AddressId);

        builder.Property(a => a.AddressId).HasColumnName("address_id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Type).HasColumnName("type").HasConversion(EnumDbValueConverters.AddressTypeConverter);
        builder.Property(a => a.Line1).HasColumnName("line1");
        builder.Property(a => a.Line2).HasColumnName("line2");
        builder.Property(a => a.City).HasColumnName("city");
        builder.Property(a => a.Region).HasColumnName("region");
        builder.Property(a => a.PostalCode).HasColumnName("postal_code");
        builder.Property(a => a.CountryCode).HasColumnName("country_code");
        builder.Property(a => a.Phone).HasColumnName("phone");
        builder.Property(a => a.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        // "At most one default Address per (user_id, type)" (ddd-model.md invariant) enforced
        // at the database layer as a backstop, not the primary enforcement mechanism (see
        // UserProfile.ClearDefaultsOfType for the application-layer aggregate invariant).
        builder.HasIndex(a => new { a.UserId, a.Type })
            .HasDatabaseName("idx_addresses_one_default_per_type")
            .IsUnique()
            .HasFilter("is_default");

        builder.HasIndex(a => a.UserId).HasDatabaseName("idx_addresses_user_id");
    }
}
