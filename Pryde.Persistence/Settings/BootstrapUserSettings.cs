namespace Pryde.Persistence.Settings;

public sealed class BootstrapUsersSettings
{
    public const string SectionName = "BootstrapUsers";

    public BootstrapUserSettings SuperAdmin { get; set; } = new();
    public BootstrapUserSettings Admin { get; set; } = new();
}

public sealed class BootstrapUserSettings
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}