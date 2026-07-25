namespace Pryde.Services.Settings;

public sealed class VehicleUploadSettings
{
    public const string SectionName = "VehicleUploads";

    public long VehicleImageMaxBytes { get; init; } = 10 * 1024 * 1024;
    public long WalkAroundVideoMaxBytes { get; init; } = 50 * 1024 * 1024;
    public long VehicleDocumentMaxBytes { get; init; } = 10 * 1024 * 1024;
}
