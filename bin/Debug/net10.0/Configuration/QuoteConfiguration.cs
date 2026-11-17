
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class QuoteConfiguration
    : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> entity)
    {
        entity.ToTable("quotes");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.TradingMnemonic)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.Symbol)
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(x => x.IsActive)
            .IsRequired();

        // Firm ownership
        entity.HasOne(x => x.Firm)
            .WithMany()
            .HasForeignKey(x => x.FirmId)
            .OnDelete(DeleteBehavior.Restrict);

        // Latest submission reference.
        // One submission may be associated with multiple quotes,
        // particularly when processing a Mass Quote.
        entity.HasOne<QuoteSubmission>()
            .WithMany()
            .HasForeignKey(x => x.LatestSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query indexes
        entity.HasIndex(x => x.FirmId);

        entity.HasIndex(x => new
        {
            x.FirmId,
            x.TradingMnemonic,
            x.Symbol,
            x.OrderBook
        });

        entity.HasIndex(x => x.LatestSubmissionId);
    }
}
