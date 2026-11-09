using System.Net;
using System.Net.Http.Json;
using AspNet.MasterPass.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AspNet.MasterPass.Tests;

public sealed class MasterPassLoginTests
{
    private const string UserName = "user-1";
    private const string UserEmail = "user@example.com";
    private const string UserPassword = "User#12345";
    private const string MasterPassword = "master-secret";

    [Fact]
    public async Task Real_password_still_works_and_is_not_flagged_as_master_pass()
    {
        using var factory = new TestApplicationFactory();
        using var client = await CreateSeededClientAsync(factory);

        var login = await client.PostAsJsonAsync("/login", new { userName = UserName, password = UserPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetFromJsonAsync<MeResponse>("/me");
        Assert.NotNull(me);
        Assert.False(me!.MasterPass);
    }

    [Fact]
    public async Task Master_password_signs_in_and_flags_the_session()
    {
        using var factory = new TestApplicationFactory();
        using var client = await CreateSeededClientAsync(factory);

        var login = await client.PostAsJsonAsync("/login", new { userName = UserName, password = MasterPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetFromJsonAsync<MeResponse>("/me");
        Assert.NotNull(me);
        Assert.True(me!.MasterPass);
    }

    [Fact]
    public async Task Master_pass_session_cannot_reach_real_password_only_endpoints()
    {
        using var factory = new TestApplicationFactory();
        using var client = await CreateSeededClientAsync(factory);

        await client.PostAsJsonAsync("/login", new { userName = UserName, password = MasterPassword });

        var response = await client.GetAsync("/real-only");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Real_password_session_can_reach_real_password_only_endpoints()
    {
        using var factory = new TestApplicationFactory();
        using var client = await CreateSeededClientAsync(factory);

        await client.PostAsJsonAsync("/login", new { userName = UserName, password = UserPassword });

        var response = await client.GetAsync("/real-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        using var factory = new TestApplicationFactory();
        using var client = await CreateSeededClientAsync(factory);

        var login = await client.PostAsJsonAsync("/login", new { userName = UserName, password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Master_password_is_rejected_for_admin_accounts()
    {
        using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var seed = await client.PostAsJsonAsync("/seed", new
        {
            userName = "admin-1",
            email = "admin@example.com",
            password = UserPassword
        });
        seed.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/login", new { userName = "admin-1", password = MasterPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Master_password_accepts_a_hashed_secret()
    {
        using var baseFactory = new TestApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<MasterPassOptions>(options =>
                options.Password = new Microsoft.AspNetCore.Identity.PasswordHasher<object>()
                    .HashPassword(new object(), MasterPassword))));

        using var client = await CreateSeededClientAsync(factory);

        var login = await client.PostAsJsonAsync("/login", new { userName = UserName, password = MasterPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Disabled_master_password_is_rejected()
    {
        using var baseFactory = new TestApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<MasterPassOptions>(options => options.Enabled = false)));

        using var client = await CreateSeededClientAsync(factory);

        var login = await client.PostAsJsonAsync("/login", new { userName = UserName, password = MasterPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public void Production_environment_refuses_to_start_with_a_configured_master_password()
    {
        using var factory = new TestApplicationFactory();
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Record.Exception(() => production.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("master password", exception!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_environment_works_when_explicitly_allowed()
    {
        using var baseFactory = new TestApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureServices(services =>
                services.Configure<MasterPassOptions>(options => options.AllowInProduction = true)));

        using var client = await CreateSeededClientAsync(factory);

        var login = await client.PostAsJsonAsync("/login", new { userName = UserName, password = MasterPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task<HttpClient> CreateSeededClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();

        var seed = await client.PostAsJsonAsync("/seed", new
        {
            userName = UserName,
            email = UserEmail,
            password = UserPassword
        });

        seed.EnsureSuccessStatusCode();

        return client;
    }
}