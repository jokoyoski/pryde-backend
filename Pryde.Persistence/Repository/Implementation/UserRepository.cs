using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class UserRepository(PrydeDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id,CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(
            u => u.Id == id,cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email,CancellationToken cancellationToken = default)
    {
        email = email.Trim().ToLower();

        return await context.Users
            .FirstOrDefaultAsync(
            u => u.Email.ToLower() == email, cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberAsync( string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(
            u => u.PhoneNumber == phoneNumber,
            cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync( string email,string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        email = email.Trim().ToLower();

        return await context.Users.AnyAsync(e => e.Email.ToLower() == email || (!string.IsNullOrWhiteSpace(phoneNumber) &&
        e.PhoneNumber == phoneNumber), cancellationToken);
    }

    public async Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        return user;
    }

    public Task UpdateAsync(User user,CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user,CancellationToken cancellationToken = default)
    {
        context.Users.Remove(user);
        return Task.CompletedTask;
    }
}