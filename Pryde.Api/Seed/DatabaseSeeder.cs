using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Settings;
using Pryde.Services.Security.Interface;

namespace Pryde.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<PrydeDbContext>();

        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher>();

        var settings = scope.ServiceProvider
            .GetRequiredService<IOptions<BootstrapUsersSettings>>()
            .Value;

        var configuration = scope.ServiceProvider
            .GetRequiredService<IConfiguration>();

        var resetExistingPasswords =
            configuration.GetValue<bool>("ResetBootstrapPasswords");

        Validate(settings);

        await SeedUserAsync(
            context,
            passwordHasher,
            settings.SuperAdmin,
            "SuperAdmin",
            resetExistingPasswords,
            cancellationToken);

        await SeedUserAsync(
            context,
            passwordHasher,
            settings.Admin,
            "Admin",
            resetExistingPasswords,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUserAsync(
        PrydeDbContext context,
        IPasswordHasher passwordHasher,
        BootstrapUserSettings settings,
        string roleName,
        bool resetExistingPassword,
        CancellationToken cancellationToken)
    {
        var email = settings.Email
            .Trim()
            .ToLowerInvariant();

        var existingUser = await context.Users
            .FirstOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);

        if (existingUser is not null)
        {
            if (resetExistingPassword)
            {
                existingUser.PasswordHash =
                    passwordHasher.Hash(settings.Password);

                existingUser.IsEmailVerified = true;
                existingUser.IsPhoneNumberVerified = true;
                existingUser.Status = UserStatus.Active;
            }

            await EnsureRoleAsync(
                context,
                existingUser,
                roleName,
                cancellationToken);

            return;
        }

        var role = await context.Roles
            .FirstOrDefaultAsync(
                role => role.Name == roleName,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"{roleName} role not found.");

        var user = new User
        {
            Email = email,
            PhoneNumber = settings.PhoneNumber.Trim(),
            PasswordHash = passwordHasher.Hash(settings.Password),
            IsEmailVerified = true,
            IsPhoneNumberVerified = true,
            IsTwoFactorEnabled = false,
            Status = UserStatus.Active
        };

        context.Users.Add(user);

        context.Profiles.Add(new Profile
        {
            User = user,
            FirstName = settings.FirstName.Trim(),
            LastName = settings.LastName.Trim()
        });

        context.UserRoles.Add(new UserRole
        {
            User = user,
            Role = role
        });
    }

    private static async Task EnsureRoleAsync(
        PrydeDbContext context,
        User user,
        string roleName,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .FirstOrDefaultAsync(
                role => role.Name == roleName,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"{roleName} role not found.");

        var alreadyAssigned = await context.UserRoles
            .AnyAsync(
                userRole =>
                    userRole.UserId == user.Id &&
                    userRole.RoleId == role.Id,
                cancellationToken);

        if (alreadyAssigned)
        {
            return;
        }

        context.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });
    }

    private static void Validate(
        BootstrapUsersSettings settings)
    {
        ValidateUser(
            settings.SuperAdmin,
            "SuperAdmin");

        ValidateUser(
            settings.Admin,
            "Admin");
    }

    private static void ValidateUser(
        BootstrapUserSettings user,
        string userName)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException(
                $"{userName} bootstrap email is missing.");
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            throw new InvalidOperationException(
                $"{userName} bootstrap phone number is missing.");
        }

        if (string.IsNullOrWhiteSpace(user.Password))
        {
            throw new InvalidOperationException(
                $"{userName} bootstrap password is missing.");
        }

        if (string.IsNullOrWhiteSpace(user.FirstName))
        {
            throw new InvalidOperationException(
                $"{userName} bootstrap first name is missing.");
        }

        if (string.IsNullOrWhiteSpace(user.LastName))
        {
            throw new InvalidOperationException(
                $"{userName} bootstrap last name is missing.");
        }
    }
}