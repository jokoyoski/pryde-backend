using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class DojahKycConfigResponseDto
{
    public string AppId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string ShareableLink { get; set; } = string.Empty;
    public string WidgetId { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public KycStatus Status { get; set; }
}
