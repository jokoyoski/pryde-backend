using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class UserRepository(PrydeDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == id,
                cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);

        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        phoneNumber = NormalizePhoneNumber(phoneNumber);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.PhoneNumber == phoneNumber,
                cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string email,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        phoneNumber = NormalizePhoneNumber(phoneNumber);

        return await context.Users.AnyAsync(
            user => user.Email == email ||
                   (!string.IsNullOrWhiteSpace(phoneNumber) &&
                    user.PhoneNumber == phoneNumber),
            cancellationToken);
    }

    public async Task CreateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        user.Email = NormalizeEmail(user.Email);
        user.PhoneNumber = NormalizePhoneNumber(user.PhoneNumber);

        await context.Users.AddAsync(user, cancellationToken);
    }

    public void Update(User user)
    {
        user.Email = NormalizeEmail(user.Email);
        user.PhoneNumber = NormalizePhoneNumber(user.PhoneNumber);

        context.Users.Update(user);
    }

    public void Delete(User user)
    {
        context.Users.Remove(user);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        return phoneNumber?.Trim();
    }
}