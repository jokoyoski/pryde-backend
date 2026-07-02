using Microsoft.AspNetCore.Http;

namespace Pryde.Contracts.RequestModels;

public sealed class KycDocumentUploadRequest
{
    public IFormFile? BiometricVerification { get; init; }

    public IFormFile? DriverLicense { get; init; }

    public IFormFile? SecondaryIdentification { get; init; }
}