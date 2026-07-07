using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Services.Security.Interface;

namespace Pryde.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<PrydeDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedSuperAdminAsync(context, passwordHasher);
        await SeedAdminAsync(context, passwordHasher);

        await context.SaveChangesAsync();
    }

    private static async Task SeedSuperAdminAsync(
        PrydeDbContext context,
        IPasswordHasher passwordHasher)
    {
        const string email = "superadmin@pryde.ng";

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin")
            ?? throw new InvalidOperationException("SuperAdmin role not found.");

        var user = new User
        {
            Email = email,
            PhoneNumber = "07011221122",
            PasswordHash = passwordHasher.Hash("superadmin@pryde"),
            IsEmailVerified = true,
            IsPhoneNumberVerified = true,
            IsTwoFactorEnabled = false,
            Status = UserStatus.Active
        };

        context.Users.Add(user);

        context.Profiles.Add(new Profile
        {
            User = user,
            FirstName = "Super",
            LastName = "Admin"
        });

        context.UserRoles.Add(new UserRole
        {
            User = user,
            Role = role
        });
    }

    private static async Task SeedAdminAsync(
        PrydeDbContext context,
        IPasswordHasher passwordHasher)
    {
        const string email = "admin@pryde.ng";

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin")
            ?? throw new InvalidOperationException("Admin role not found.");

        var user = new User
        {
            Email = email,
            PhoneNumber = "08011221122",
            PasswordHash = passwordHasher.Hash("admin@pryde"),
            IsEmailVerified = true,
            IsPhoneNumberVerified = true,
            IsTwoFactorEnabled = false,
            Status = UserStatus.Active
        };

        context.Users.Add(user);

        context.Profiles.Add(new Profile
        {
            User = user,
            FirstName = "Pryde",
            LastName = "Admin"
        });

        context.UserRoles.Add(new UserRole
        {
            User = user,
            Role = role
        });
    }
}