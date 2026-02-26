using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Infrastructure.Security;

namespace UtilityMenuSite.Data.Context;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

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

        // Ensure a corresponding AppUsers record exists for the admin Identity user.
        // Without this, IUserService.GetByIdentityIdAsync returns null and admin pages fail.
        var adminProfile = await db.AppUsers.FirstOrDefaultAsync(u => u.IdentityId == admin.Id);
        if (adminProfile is null)
        {
            db.AppUsers.Add(new User
            {
                IdentityId = admin.Id,
                Email = adminEmail,
                DisplayName = "Admin",
                ApiToken = ApiTokenGenerator.Generate(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
