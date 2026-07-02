using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Models;

namespace Pryde.Services.Storage.Interface;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        FileCategory category,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<string> GetReadUrlAsync(
        string fileKey,
        FileCategory category,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string fileKey,
        FileCategory category,
        CancellationToken cancellationToken = default);
}