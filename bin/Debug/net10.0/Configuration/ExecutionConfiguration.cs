using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class ExecutionConfiguration
    : IEntityTypeConfiguration<Execution>
{
    public void Configure(EntityTypeBuilder<Execution> entity)
    {
        entity.ToTable("executions");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.ExecId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.ExecType)
            .HasMaxLength(2)
            .IsRequired();

        entity.Property(x => x.OrdStatus)
            .HasMaxLength(2)
            .IsRequired();

        entity.Property(x => x.Symbol)
            .HasMaxLength(32);

        entity.Property(x => x.Side)
            .HasMaxLength(1);

        entity.Property(x => x.LastQty)
            .HasPrecision(20, 6);

        entity.Property(x => x.LastPx)
            .HasPrecision(20, 8);

        entity.Property(x => x.CumQty)
            .HasPrecision(20, 6);

        entity.Property(x => x.LeavesQty)
            .HasPrecision(20, 6);

        entity.Property(x => x.AvgPx)
            .HasPrecision(20, 8);

        // Prevent duplicate processing of the same execution
        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.HasIndex(x => new { x.SessionId, x.ExecId })
            .IsUnique();

        // One order can have multiple executions
        entity.HasOne(x => x.TradingOrder)
            .WithMany(x => x.Executions)
            .HasForeignKey(x => x.TradingOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.TradingOrderId);

        entity.Property(x => x.MdEntryId)
    .HasMaxLength(128);

    entity.Property(x => x.OrderCapacity)
        .HasMaxLength(1);

    entity.Property(x => x.TrdMatchId)
        .HasMaxLength(128);

    entity.Property(x => x.SecondaryTradeId)
        .HasMaxLength(128);

    entity.Property(x => x.ExecRestatementReason)
        .HasMaxLength(10);

    entity.Property(x => x.OrdRejReason)
        .HasMaxLength(10);

    entity.Property(x => x.EventCategory)
    .HasMaxLength(20)
    .IsRequired();        
            
    entity.HasOne(x => x.ReferencedExecution)
        .WithMany(x => x.RelatedExecutions)
        .HasForeignKey(x => x.ReferencedExecutionId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasIndex(x => x.ReferencedExecutionId);

    }
}