using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Entities;
using TradingBackend.Api.Identity;

namespace TradingBackend.Api.Data;

public class TradingDbContext
    : IdentityDbContext<ApplicationUser>
{
    public TradingDbContext(
        DbContextOptions<TradingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Firm> Firms => Set<Firm>();

    public DbSet<TradingOrder> Orders => Set<TradingOrder>();

    public DbSet<Execution> Executions => Set<Execution>();

    public DbSet<FixMessageLog> FixMessageLogs
        => Set<FixMessageLog>();

    public DbSet<OrderIdentifier> OrderIdentifiers
        => Set<OrderIdentifier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TradingDbContext).Assembly
        );
    }
}