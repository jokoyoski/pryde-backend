namespace Pryde.Services.Service.Interface;

public interface ISmileIdKycService
{
    Task ReconcilePendingAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ProcessCallbackAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
