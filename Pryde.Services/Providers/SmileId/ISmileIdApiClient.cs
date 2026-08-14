namespace Pryde.Services.Providers.SmileId;

public interface ISmileIdApiClient
{
    Task<SmileIdLinkResponse> CreateSingleUseLinkAsync(
        SmileIdLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<SmileIdJobStatusResponse> GetJobStatusAsync(
        string userId,
        string jobId,
        CancellationToken cancellationToken = default);

    bool ValidateSignature(string timestamp, string signature);
}

public sealed record SmileIdAuthentication(string Timestamp, string Signature);
