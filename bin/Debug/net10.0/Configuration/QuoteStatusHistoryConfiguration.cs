
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class QuoteStatusHistoryConfiguration
    : IEntityTypeConfiguration<QuoteStatusHistory>
{
    public void Configure(EntityTypeBuilder<QuoteStatusHistory> entity)
    {
        entity.ToTable("quote_status_history");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.QuoteStatus)
            .HasMaxLength(2)
            .IsRequired();

        entity.Property(x => x.QuoteRejectReason)
            .HasMaxLength(10);

        entity.Property(x => x.MsgType)
            .HasMaxLength(2)
            .IsRequired();

        entity.Property(x => x.ApplID)
            .HasMaxLength(128);

        entity.Property(x => x.BidID)
            .HasMaxLength(128);

        entity.Property(x => x.OfferID)
            .HasMaxLength(128);

        entity.Property(x => x.Text)
            .HasColumnType("text");

        entity.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<QuoteSubmission>()
            .WithMany()
            .HasForeignKey(x => x.QuoteSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<QuoteEntryRevision>()
            .WithMany()
            .HasForeignKey(x => x.QuoteEntryRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<FixMessageLog>()
            .WithMany()
            .HasForeignKey(x => x.FixMessageLogId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.QuoteId);

        entity.HasIndex(x => x.QuoteSubmissionId);

        entity.HasIndex(x => x.QuoteEntryRevisionId);

        entity.HasIndex(x => x.FixMessageLogId);

        entity.HasIndex(x => new
        {
            x.QuoteId,
            x.CreatedAtUtc
        });
    }
}
