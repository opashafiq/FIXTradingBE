
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class QuoteSubmissionConfiguration
    : IEntityTypeConfiguration<QuoteSubmission>
{
    public void Configure(EntityTypeBuilder<QuoteSubmission> entity)
    {
        entity.ToTable("quote_submissions");

        entity.HasKey(x => x.Id);

        // FIX session and message identity
        entity.Property(x => x.SessionId)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.MsgType)
            .HasMaxLength(2)
            .IsRequired();

        // CHSE quote identifiers
        entity.Property(x => x.QuoteID)
            .HasMaxLength(128);

        entity.Property(x => x.QuoteMsgID)
            .HasMaxLength(128);

        entity.Property(x => x.QuoteReqID)
            .HasMaxLength(128);

        entity.Property(x => x.ApplID)
            .HasMaxLength(128);

        // Trading identity
        entity.Property(x => x.Account)
            .HasMaxLength(64);

        // Local transport submission state
        entity.Property(x => x.SubmissionStatus)
            .HasMaxLength(32)
            .IsRequired();

        // Firm ownership
        entity.HasOne(x => x.Firm)
            .WithMany()
            .HasForeignKey(x => x.FirmId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lookup indexes
        entity.HasIndex(x => x.FirmId);

        entity.HasIndex(x => new
        {
            x.SessionId,
            x.MsgType
        });

        entity.HasIndex(x => x.QuoteMsgID);

        entity.HasIndex(x => x.QuoteID);

        entity.HasIndex(x => x.CreatedAtUtc);
    }
}
