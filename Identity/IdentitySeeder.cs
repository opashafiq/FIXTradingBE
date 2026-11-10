using Microsoft.AspNetCore.Identity;

namespace TradingBackend.Api.Identity;

public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in RoleConstants.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(x => x.Description));

                    throw new InvalidOperationException(
                        $"Failed to create role '{roleName}': {errors}");
                }
            }
        }
    }

    public static async Task SeedInitialSystemAdminAsync(
        UserManager<ApplicationUser> userManager)
    {
        // Do not create another initial administrator.
        var existingAdmins =
            await userManager.GetUsersInRoleAsync(
                RoleConstants.SystemAdmin);

        if (existingAdmins.Count > 0)
            return;

        // Read bootstrap credentials from environment variables.
        var username =
            Environment.GetEnvironmentVariable(
                "INITIAL_ADMIN_USERNAME");

        var email =
            Environment.GetEnvironmentVariable(
                "INITIAL_ADMIN_EMAIL");

        var password =
            Environment.GetEnvironmentVariable(
                "INITIAL_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Initial System Administrator credentials are missing. " +
                "Configure INITIAL_ADMIN_USERNAME, " +
                "INITIAL_ADMIN_EMAIL and INITIAL_ADMIN_PASSWORD.");
        }

        var admin = new ApplicationUser
        {
            UserName = username,
            Email = email,
            FirmId = null,
            EmailConfirmed = false
        };

        var createResult =
            await userManager.CreateAsync(admin, password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join(
                ", ",
                createResult.Errors.Select(x => x.Description));

            throw new InvalidOperationException(
                $"Failed to create initial System Administrator: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(
            admin,
            RoleConstants.SystemAdmin);

        if (!roleResult.Succeeded)
        {
            // Remove the user if role assignment fails.
            await userManager.DeleteAsync(admin);

            var errors = string.Join(
                ", ",
                roleResult.Errors.Select(x => x.Description));

            throw new InvalidOperationException(
                $"Failed to assign SystemAdmin role: {errors}");
        }
    }
}