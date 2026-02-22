using Microsoft.AspNetCore.Identity;
using UtilityMenuSite.Data.Models;

namespace UtilityMenuSite.Data.Context;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // Seed roles
        string[] roles = ["Admin", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed default admin user in development
        const string adminEmail = "admin@utilitymenu.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin@123456");
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
