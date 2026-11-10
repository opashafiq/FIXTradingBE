using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Configurations;

public class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne(x => x.Firm)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.FirmId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CHSEWorkstationId)
            .HasMaxLength(50);

        builder.HasIndex(x => x.CHSEWorkstationId)
            .IsUnique()
            .HasFilter("\"CHSEWorkstationId\" IS NOT NULL");

        builder.Property(x => x.IsActive)
        .IsRequired()
        .HasDefaultValue(true);    
    }
}