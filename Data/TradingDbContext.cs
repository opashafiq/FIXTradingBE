using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Entities;
using TradingBackend.Api.Configurations;

namespace TradingBackend.Api.Data;

public class TradingDbContext : DbContext
{
    public TradingDbContext(
        DbContextOptions<TradingDbContext> options)
        : base(options)
    {
    }

    public DbSet<TradingOrder> Orders => Set<TradingOrder>();

    public DbSet<Execution> Executions => Set<Execution>();

    public DbSet<FixMessageLog> FixMessageLogs => Set<FixMessageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TradingDbContext).Assembly
        );
    }
}