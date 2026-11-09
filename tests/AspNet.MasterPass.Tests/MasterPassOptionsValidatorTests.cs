using AspNet.MasterPass.Internal;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AspNet.MasterPass.Tests;

public sealed class MasterPassOptionsValidatorTests
{
    [Fact]
    public void Production_is_rejected_when_a_password_is_configured()
    {
        var validator = new MasterPassOptionsValidator(new FakeEnvironment("Production"));

        var result = validator.Validate(null, new MasterPassOptions { Password = "secret" });

        Assert.True(result.Failed);
        Assert.Contains("master password", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_is_allowed_when_explicitly_enabled()
    {
        var validator = new MasterPassOptionsValidator(new FakeEnvironment("Production"));

        var result = validator.Validate(null, new MasterPassOptions
        {
            Password = "secret",
            AllowInProduction = true
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Production_is_fine_without_a_password()
    {
        var validator = new MasterPassOptionsValidator(new FakeEnvironment("Production"));

        var result = validator.Validate(null, new MasterPassOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Staging_is_allowed()
    {
        var validator = new MasterPassOptionsValidator(new FakeEnvironment("Staging"));

        var result = validator.Validate(null, new MasterPassOptions { Password = "secret" });

        Assert.True(result.Succeeded);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public FakeEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}