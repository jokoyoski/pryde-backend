namespace Pryde.Services.Storage.Models;

public sealed record FileUploadResult(
    string FileKey,
    string PublicUrl);