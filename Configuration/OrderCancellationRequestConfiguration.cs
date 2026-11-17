using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class OrderCancellationRequestConfiguration
    : IEntityTypeConfiguration<OrderCancellationRequest>
{
    
    public void Configure(
        EntityTypeBuilder<OrderCancellationRequest> entity)
    {
        Console.WriteLine(
    ">>> OrderCancellationRequestConfiguration EXECUTED <<<");
        entity.ToTable("order_cancellation_requests");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.ClOrdId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.OrigClOrdId)
            .HasMaxLength(128);

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

        // A cancellation ClOrdID must be unique within a FIX session.
        entity.HasIndex(x => new
        {
            x.SessionId,
            x.ClOrdId
        }).IsUnique();

        // Useful for finding cancellation requests by order and status.
        entity.HasIndex(x => new
        {
            x.TradingOrderId,
            x.Status
        });

        // Only one outstanding cancellation request per order.
        entity.HasIndex(x => x.TradingOrderId)
            .IsUnique()
            .HasDatabaseName(
                "IX_order_cancellation_requests_TradingOrderId_Active")
            .HasFilter(
                "\"Status\" IN ('PendingSubmission', 'SubmittedToFix', 'SubmissionUnknown')");

                foreach (var index in entity.Metadata.GetIndexes())
{
    Console.WriteLine(
        $"CONFIG INDEX: {index.GetDatabaseName()}, " +
        $"Unique: {index.IsUnique}, " +
        $"Filter: {index.GetFilter()}");
}
            
            
    }
}
