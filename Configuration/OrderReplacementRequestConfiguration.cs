
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class OrderReplacementRequestConfiguration
    : IEntityTypeConfiguration<OrderReplacementRequest>
{
    public void Configure(
        EntityTypeBuilder<OrderReplacementRequest> entity)
    {
        entity.ToTable("order_replacement_requests");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.ClOrdId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.OrigClOrdId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.OrderQty)
            .HasPrecision(20, 6)
            .IsRequired();

        entity.Property(x => x.Price)
            .HasPrecision(20, 8);

        entity.Property(x => x.StopPx)
            .HasPrecision(20, 8);

        entity.Property(x => x.TimeInForce)
            .HasMaxLength(4);

        entity.Property(x => x.ExpireDate)
            .HasColumnType("date");

        entity.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(x => x.SubmissionError)
            .HasColumnType("text");

        entity.Property(x => x.CreatedAtUtc)
            .IsRequired();

        entity.HasOne(x => x.TradingOrder)
            .WithMany()
            .HasForeignKey(x => x.TradingOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.SessionId, x.ClOrdId })
            .IsUnique();

        entity.HasIndex(x => new
        {
            x.TradingOrderId,
            x.Status
        });

        entity.HasIndex(x => x.TradingOrderId)
            .IsUnique()
            .HasDatabaseName(
                "IX_order_replacement_requests_TradingOrderId_Active")
            .HasFilter(
                "\"Status\" IN ('PendingSubmission', " +
                "'SubmittedToFix', 'SubmissionUnknown', 'PendingReplace')");
    }
}
