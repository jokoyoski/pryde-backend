using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Pryde.Tests.TestInfrastructure;

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = string.Empty;
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } =
        new NullFileProvider();
}
