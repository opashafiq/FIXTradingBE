
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class OrderCancelRejectConfiguration
    : IEntityTypeConfiguration<OrderCancelReject>
{
    public void Configure(EntityTypeBuilder<OrderCancelReject> entity)
    {
        entity.ToTable("order_cancel_rejects");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.OrderId)
            .HasMaxLength(128);

        entity.Property(x => x.ClOrdId)
            .HasMaxLength(128);

        entity.Property(x => x.OrigClOrdId)
            .HasMaxLength(128);

        entity.Property(x => x.OrdStatus)
            .HasMaxLength(2);

        entity.Property(x => x.Text)
            .HasColumnType("text");

        entity.Property(x => x.ReceivedAtUtc)
            .IsRequired();

        entity.HasOne(x => x.TradingOrder)
            .WithMany()
            .HasForeignKey(x => x.TradingOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.FixMessageLog)
            .WithOne()
            .HasForeignKey<OrderCancelReject>(
                x => x.FixMessageLogId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.TradingOrderId);

        entity.HasIndex(x => new
        {
            x.SessionId,
            x.ClOrdId
        });

        entity.HasIndex(x => x.FixMessageLogId)
            .IsUnique();
    }
}
