using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class TradingOrderConfiguration
    : IEntityTypeConfiguration<TradingOrder>
{
    public void Configure(EntityTypeBuilder<TradingOrder> entity)
    {
        entity.ToTable("orders");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.ClOrdId)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.Symbol)
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(x => x.Side)
            .HasMaxLength(1)
            .IsRequired();

        entity.Property(x => x.OrdStatus).HasMaxLength(32);

        entity.Property(x => x.OrderQty)
            .HasPrecision(20, 6);

        entity.Property(x => x.Price)
            .HasPrecision(20, 8);

        entity.Property(x => x.LeavesQty)
            .HasPrecision(20, 6);

        entity.Property(x => x.CumQty)
            .HasPrecision(20, 6);

        entity.Property(x => x.AvgPx)
            .HasPrecision(20, 8);

        entity.HasIndex(x => x.ExchangeOrderId);

        entity.HasIndex(x => x.Symbol);
    }
}