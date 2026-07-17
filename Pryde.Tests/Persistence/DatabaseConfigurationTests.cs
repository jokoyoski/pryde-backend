using Microsoft.Extensions.Configuration;
using Pryde.Persistence.Extension;

namespace Pryde.Tests.Persistence;

public class DatabaseConfigurationTests
{
    [Fact]
    public void ConnectionBuilderTrimsTextFieldsButPreservesPassword()
    {
        const string password = " password with edge spaces ";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PrydeConnection:Host"] = " db.example.com ",
            ["PrydeConnection:Port"] = "5432",
            ["PrydeConnection:Database"] = " pryde-dev ",
            ["PrydeConnection:Username"] = " neondb_owner ",
            ["PrydeConnection:Password"] = password,
            ["PrydeConnection:SslMode"] = " Require "
        });

        var builder = configuration.GetDbConnectionStringBuilder();

        Assert.Equal("db.example.com", builder.Host);
        Assert.Equal("pryde-dev", builder.Database);
        Assert.Equal("neondb_owner", builder.Username);
        Assert.Equal(password, builder.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("example.com:5432")]
    [InlineData("\"example.com\"")]
    public void ConnectionBuilderRejectsInvalidHost(string host)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PrydeConnection:Host"] = host,
            ["PrydeConnection:Port"] = "5432",
            ["PrydeConnection:Database"] = "pryde-dev",
            ["PrydeConnection:Username"] = "neondb_owner",
            ["PrydeConnection:Password"] = "secret",
            ["PrydeConnection:SslMode"] = "Require"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetDbConnectionStringBuilder);

        Assert.DoesNotContain("secret", exception.Message);
    }

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
