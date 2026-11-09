using System.Security.Claims;
using AspNet.MasterPass;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? "Data Source=masterpass-sample.db"));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<SampleDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMasterPass<IdentityUser>(builder.Configuration, options =>
{
    options.AllowForUser = attempt =>
        attempt.User is not IdentityUser user
        || !string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase);
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await database.Database.EnsureCreatedAsync();

    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    if (await users.FindByNameAsync("demo") is null)
    {
        await users.CreateAsync(
            new IdentityUser { UserName = "demo", Email = "demo@example.com", EmailConfirmed = true },
            "Demo#12345");
    }

    if (await users.FindByNameAsync("admin") is null)
    {
        await users.CreateAsync(
            new IdentityUser { UserName = "admin", Email = "admin@example.com", EmailConfirmed = true },
            "Admin#12345");
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (ClaimsPrincipal user) => Results.Content(
    SamplePage.Render(user.IsMasterPassLogin(), user.Identity?.Name), "text/html"));

app.MapPost("/login", async (SignInManager<IdentityUser> signInManager, LoginRequest request) =>
{
    var result = await signInManager.PasswordSignInAsync(
        request.UserName,
        request.Password,
        isPersistent: false,
        lockoutOnFailure: false);

    return result.Succeeded ? Results.Ok() : Results.Unauthorized();
});

app.MapPost("/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Ok();
});

app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    userName = user.Identity?.Name,
    masterPass = user.IsMasterPassLogin()
}));

app.Run();

public sealed record LoginRequest(string UserName, string Password);

public sealed class SampleDbContext : IdentityDbContext<IdentityUser>
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options)
        : base(options)
    {
    }
}

internal static class SamplePage
{
    public static string Render(bool masterPass, string? userName)
    {
        var session = userName is null
            ? "Belum login."
            : $"Login sebagai <strong>{userName}</strong> "
              + (masterPass
                  ? "dengan <strong>master password</strong>."
                  : "dengan <strong>password asli</strong>.");

        return $$"""
            <!doctype html>
            <html lang="id">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AspNet.MasterPass sample</title>
              <style>
                :root { color-scheme: light dark; }
                body { font-family: system-ui, sans-serif; max-width: 42rem; margin: 3rem auto; padding: 0 1rem; line-height: 1.6; }
                code { padding: .15rem .35rem; border-radius: .25rem; background: color-mix(in srgb, currentColor 12%, transparent); }
                button { padding: .6rem 1rem; border-radius: .5rem; border: 1px solid currentColor; background: none; cursor: pointer; }
                .row { display: flex; gap: .5rem; flex-wrap: wrap; margin: 1rem 0; }
                pre { padding: 1rem; border-radius: .75rem; background: color-mix(in srgb, currentColor 8%, transparent); overflow-x: auto; }
              </style>
            </head>
            <body>
              <h1>AspNet.MasterPass</h1>
              <p>{{session}}</p>
              <div class="row">
                <button onclick="login('demo', 'Demo#12345')">Login demo (password asli)</button>
                <button onclick="login('demo', 'local-dev-master')">Login demo (master password)</button>
                <button onclick="logout()">Logout</button>
              </div>
              <p>Akun <code>admin</code> sengaja dikecualikan lewat <code>AllowForUser</code>.</p>
              <pre>curl -X POST /login -H "Content-Type: application/json" -d '{"userName":"demo","password":"local-dev-master"}'</pre>
              <script>
                async function login(userName, password) {
                  const response = await fetch('/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ userName, password })
                  });
                  location.reload();
                }
                async function logout() {
                  await fetch('/logout', { method: 'POST' });
                  location.reload();
                }
              </script>
            </body>
            </html>
            """;
    }
}