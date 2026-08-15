namespace Pryde.Services.Settings;

public sealed class VehicleDocumentSettings
{
    public const string SectionName = "VehicleDocuments";
    public const string ValidationError =
        "VehicleDocuments:MinimumValidityMonths must be greater than zero.";

    public int MinimumValidityMonths { get; init; } = 6;

    public static bool IsValid(VehicleDocumentSettings settings) =>
        settings.MinimumValidityMonths > 0;
}
