
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class QuoteSetConfiguration
    : IEntityTypeConfiguration<QuoteSet>
{
    public void Configure(EntityTypeBuilder<QuoteSet> entity)
    {
        entity.ToTable("quote_sets");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.QuoteSetID)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.UnderlyingSymbol)
            .HasMaxLength(32);

        entity.Property(x => x.TotNoQuoteEntries)
            .IsRequired();

        // Parent Mass Quote submission
        entity.HasOne<QuoteSubmission>()
            .WithMany()
            .HasForeignKey(x => x.QuoteSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lookup indexes
        entity.HasIndex(x => x.QuoteSubmissionId);

        entity.HasIndex(x => new
        {
            x.QuoteSubmissionId,
            x.QuoteSetID
        }).IsUnique();
    }
}
