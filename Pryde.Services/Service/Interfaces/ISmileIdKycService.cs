namespace Pryde.Services.Service.Interface;

public interface ISmileIdKycService
{
    Task ProcessCallbackAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
