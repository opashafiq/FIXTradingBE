using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class FixMessageLogConfiguration
    : IEntityTypeConfiguration<FixMessageLog>
{
    public void Configure(EntityTypeBuilder<FixMessageLog> entity)
    {
        entity.ToTable("fix_message_logs");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.MsgType)
            .HasMaxLength(10)
            .IsRequired();

        entity.Property(x => x.Direction)
            .HasMaxLength(3)
            .IsRequired();

        entity.Property(x => x.RawMessage)
            .HasColumnType("text")
            .IsRequired();

        entity.Property(x => x.ProcessingError)
            .HasColumnType("text");

        entity.Property(x => x.MsgSeqNum).IsRequired();

        entity.Property(x => x.PossDupFlag).IsRequired();

        entity.Property(x => x.PossResend).IsRequired();

        entity.Property(x => x.SendingTime).HasMaxLength(32);

        entity.Property(x => x.OrigSendingTime).HasMaxLength(32);

        entity.Property(x => x.BeginString)
            .HasMaxLength(16)
            .IsRequired();

        entity.Property(x => x.SenderCompID)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.TargetCompID)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.SessionQualifier)
            .HasMaxLength(64)
            .IsRequired();        

        entity.Property(x => x.ProcessingStatus)
        .HasMaxLength(20)
        .IsRequired();

        entity.Property(x => x.ProcessingStartedAtUtc);

        entity.HasIndex(x => new
        {
            x.SessionId,
            x.MsgSeqNum
        });

        entity.Property(x => x.RetryCount)
        .IsRequired();

        entity.Property(x => x.NextRetryAtUtc);

        entity.HasIndex(x => x.ReceivedAtUtc);

        entity.HasIndex(x => x.ClOrdId);

        entity.HasIndex(x => x.ExecId);
        entity.HasIndex(x => new
        {
            x.ProcessingStatus,
            x.NextRetryAtUtc
        });
    }
}