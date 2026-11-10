using Microsoft.AspNetCore.Identity;

namespace TradingBackend.Api.Identity;

public class IdentitySeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public IdentitySeedHostedService(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        await IdentitySeeder.SeedRolesAsync(roleManager);

        await IdentitySeeder.SeedInitialSystemAdminAsync(userManager);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}