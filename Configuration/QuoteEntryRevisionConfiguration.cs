using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class QuoteEntryRevisionConfiguration
    : IEntityTypeConfiguration<QuoteEntryRevision>
{
    public void Configure(EntityTypeBuilder<QuoteEntryRevision> entity)
    {
        entity.ToTable("quote_entry_revisions");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.QuoteEntryID)
            .HasMaxLength(64);

        entity.Property(x => x.Symbol)
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(x => x.BidPx)
            .HasPrecision(20, 8);

        entity.Property(x => x.BidSize)
            .HasPrecision(20, 6);

        entity.Property(x => x.OfferPx)
            .HasPrecision(20, 8);

        entity.Property(x => x.OfferSize)
            .HasPrecision(20, 6);

        entity.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        // Logical quote
        entity.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Exact originating FIX submission
        entity.HasOne<QuoteSubmission>()
            .WithMany()
            .HasForeignKey(x => x.QuoteSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional Mass Quote set
        entity.HasOne<QuoteSet>()
            .WithMany()
            .HasForeignKey(x => x.QuoteSetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lookup indexes
        entity.HasIndex(x => x.QuoteId);

        entity.HasIndex(x => x.QuoteSubmissionId);

        entity.HasIndex(x => x.QuoteSetId);

        entity.HasIndex(x => new
        {
            x.QuoteSubmissionId,
            x.QuoteSetId,
            x.QuoteEntryID
        });

        entity.HasIndex(x => new
        {
            x.QuoteId,
            x.CreatedAtUtc
        });
    }
}