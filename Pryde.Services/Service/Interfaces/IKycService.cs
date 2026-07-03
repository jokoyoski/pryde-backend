using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IKycService
{
    Task<KycVerificationResponseDto> UploadDocumentsAsync(
        Guid userId,
        KycDocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<KycVerificationResponseDto> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}