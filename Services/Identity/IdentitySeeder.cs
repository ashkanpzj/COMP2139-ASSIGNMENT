using System.Linq;
using Assignment1.Authorization;
using Assignment1.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Assignment1.Services.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var adminEmail = configuration["SeedAdmin:Email"] ?? "admin@eventhub.local";
        var adminPassword = configuration["SeedAdmin:Password"] ?? "Admin!123";
        var adminName = configuration["SeedAdmin:FullName"] ?? "Platform Admin";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = adminName,
                ProfilePictureUrl = null
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        foreach (var role in RoleNames.All)
        {
            if (!await userManager.IsInRoleAsync(adminUser, role))
            {
                await userManager.AddToRoleAsync(adminUser, role);
            }
        }
    }
}

