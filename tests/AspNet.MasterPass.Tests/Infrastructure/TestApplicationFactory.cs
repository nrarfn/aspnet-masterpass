using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AspNet.MasterPass.Tests.Infrastructure;

/// <summary>
/// Boots the test application in the Development environment with a fixed master password.
/// </summary>
public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}