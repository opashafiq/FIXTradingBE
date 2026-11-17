
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class BusinessMessageRejectRecordConfiguration
    : IEntityTypeConfiguration<BusinessMessageRejectRecord>
{
    public void Configure(
        EntityTypeBuilder<BusinessMessageRejectRecord> entity)
    {
        entity.ToTable("business_message_rejects");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.BusinessRejectRefID)
            .HasMaxLength(128);

        entity.Property(x => x.RefMsgType)
            .HasMaxLength(10);

        entity.Property(x => x.Text)
            .HasColumnType("text");

        entity.Property(x => x.BusinessRejectReason)
            .IsRequired();

        entity.Property(x => x.ReceivedAtUtc)
            .IsRequired();

        // One rejection record per incoming FIX message log.
        entity.HasIndex(x => x.FixMessageLogId)
            .IsUnique();

        entity.HasOne<FixMessageLog>()
            .WithMany()
            .HasForeignKey(x => x.FixMessageLogId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<TradingOrder>()
            .WithMany()
            .HasForeignKey(x => x.TradingOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports fallback correlation using FIX sequence information.
        entity.HasIndex(x => new
        {
            x.SessionId,
            x.RefSeqNum,
            x.RefMsgType
        });
    }
}
