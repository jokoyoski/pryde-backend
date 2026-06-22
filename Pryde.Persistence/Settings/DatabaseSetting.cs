namespace Pryde.Persistence.Settings;

public sealed class DatabaseSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SslMode { get; set; } = "Require";
    public string ChannelBinding { get; set; } = "Require";
}