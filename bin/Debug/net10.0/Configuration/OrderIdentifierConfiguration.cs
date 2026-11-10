using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class OrderIdentifierConfiguration
    : IEntityTypeConfiguration<OrderIdentifier>
{
    public void Configure(EntityTypeBuilder<OrderIdentifier> entity)
    {
        entity.ToTable("order_identifiers");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SessionId)
            .HasMaxLength(128);

        entity.Property(x => x.ClOrdId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.OrigClOrdId)
            .HasMaxLength(128);

        entity.Property(x => x.CreatedAtUtc)
            .IsRequired();

        entity.HasOne(x => x.TradingOrder)
            .WithMany(x => x.Identifiers)
            .HasForeignKey(x => x.TradingOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // An identifier must be unique within a FIX session.
        entity.HasIndex(x => new
        {
            x.SessionId,
            x.ClOrdId
        }).IsUnique();

        entity.HasIndex(x => x.TradingOrderId);
    }
}