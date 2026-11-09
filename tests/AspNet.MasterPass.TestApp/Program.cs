using System.Security.Claims;
using AspNet.MasterPass;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var databaseName = $"masterpass-tests-{Guid.NewGuid():N}";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase(databaseName));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<TestDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = "master-secret";
    options.AllowForUser = attempt => attempt.User is not IdentityUser user
        || !(user.Email ?? string.Empty).StartsWith("admin", StringComparison.OrdinalIgnoreCase);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/seed", async (UserManager<IdentityUser> users, SeedRequest request) =>
{
    var user = new IdentityUser
    {
        UserName = request.UserName,
        Email = request.Email,
        EmailConfirmed = true
    };

    var result = await users.CreateAsync(user, request.Password);

    return result.Succeeded
        ? Results.Ok()
        : Results.Problem(string.Join("; ", result.Errors.Select(error => error.Description)));
});

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

app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new MeResponse(
    user.Identity?.Name,
    user.IsMasterPassLogin())));

app.MapGet("/real-only", () => Results.Ok(new { ok = true }))
    .RequireAuthorization(MasterPassDefaults.RequireRealPasswordPolicy);

app.Run();

public sealed record LoginRequest(string UserName, string Password);

public sealed record SeedRequest(string UserName, string Email, string Password);

public sealed record MeResponse(string? UserName, bool MasterPass);

public sealed class TestDbContext : IdentityDbContext<IdentityUser>
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }
}

public partial class Program;