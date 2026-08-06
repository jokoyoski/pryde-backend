namespace Pryde.Services.Service.Interface;

public interface IAdminUserDeletionService
{
    Task DeleteAsync(
        Guid currentUserId,
        Guid? userId,
        string? email,
        CancellationToken cancellationToken = default);
}
