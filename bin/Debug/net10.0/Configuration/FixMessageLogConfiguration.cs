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

        entity.HasIndex(x => x.ReceivedAtUtc);

        entity.HasIndex(x => x.ClOrdId);

        entity.HasIndex(x => x.ExecId);
    }
}