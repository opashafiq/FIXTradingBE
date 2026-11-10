using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Configurations;

public class FirmConfiguration : IEntityTypeConfiguration<Firm>
{
    public void Configure(EntityTypeBuilder<Firm> builder)
    {
        builder.ToTable("Firms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirmName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CHSEFirmId)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.CHSEFirmId)
            .IsUnique();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();
    }
}