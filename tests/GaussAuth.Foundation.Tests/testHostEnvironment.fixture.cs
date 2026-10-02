using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Foundation.Tests;

/// <summary>A minimal <see cref="IHostEnvironment"/> with a chosen environment name.</summary>
internal sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "GaussAuth.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
