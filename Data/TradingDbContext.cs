using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Configurations;
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

    public DbSet<BusinessMessageRejectRecord> BusinessMessageRejects
        => Set<BusinessMessageRejectRecord>();
    public DbSet<OrderCancelReject> OrderCancelRejects
    => Set<OrderCancelReject>();
    public DbSet<OrderCancellationRequest> OrderCancellationRequests
    => Set<OrderCancellationRequest>();
    public DbSet<OrderReplacementRequest> OrderReplacementRequests
    => Set<OrderReplacementRequest>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TradingDbContext).Assembly
        );

    }
}