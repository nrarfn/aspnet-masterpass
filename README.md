# AspNet.MasterPass

[English](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.md) ·
[Bahasa Indonesia](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.id.md) ·
[中文](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/AspNet.MasterPass.svg)](https://www.nuget.org/packages/AspNet.MasterPass)
[![NuGet downloads](https://img.shields.io/nuget/dt/AspNet.MasterPass.svg)](https://www.nuget.org/packages/AspNet.MasterPass)
[![ci](https://github.com/nrarfn/aspnet-masterpass/actions/workflows/ci.yml/badge.svg)](https://github.com/nrarfn/aspnet-masterpass/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

One master password that signs you in to any ASP.NET Core Identity account.

Built for the two situations where a support engineer or a developer genuinely needs to become
another user: reproducing a bug that only appears on that account, and helping a locked-out user
without asking for their password. Every account keeps its normal password; the master password is
a second valid password that only works where you allow it.

Ported from the idea of [laravel-MasterPass](https://github.com/imanghafoori1/laravel-MasterPass),
rebuilt for ASP.NET Core Identity.

## Requirements

- ASP.NET Core Identity (`AddIdentity`, `AddDefaultIdentity`, or `AddIdentityCore` + `AddSignInManager`)
- Cookie authentication, which Identity sets up for you
- .NET 8, .NET 9 or .NET 10

## Install

```bash
dotnet add package AspNet.MasterPass
```

Register the feature **after** Identity:

```csharp
using AspNet.MasterPass;

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMasterPass<IdentityUser>(builder.Configuration);
```

Then provide the secret. It can be a plaintext password or an ASP.NET Core Identity password hash:

```json
{
  "MasterPass": {
    "Password": "a-long-and-boring-master-password"
  }
}
```

```bash
# or, preferred in containers
MasterPass__Password=a-long-and-boring-master-password
```

Without a secret the package does nothing at all: the decorator simply delegates to the default
Identity password hasher.

## Guard rails

Production is blocked by default. If a master password is configured while the host environment is
`Production`, the application refuses to start. Opt in only when you really mean it:

```csharp
builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = builder.Configuration["MasterPass:Password"];
    options.AllowInProduction = true;
});
```

Keep administrative accounts on their own password. This is what `AllowForUser` is for:

```csharp
builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = builder.Configuration["MasterPass:Password"];

    options.AllowForUser = attempt =>
        attempt.User is IdentityUser user &&
        !user.Email!.EndsWith("@yourcompany.com", StringComparison.OrdinalIgnoreCase);
});
```

`AllowForUser` runs after the master password already matched, so it is a policy gate, not a second
password check.

## Knowing which sessions used it

The authentication ticket carries a `masterpass` claim when the session started with the master
password:

```csharp
if (User.IsMasterPassLogin())
{
    // show a warning banner, hide destructive actions, ...
}
```

```html
<master-pass-only>
    <p>You are signed in as another user. Actions are logged.</p>
</master-pass-only>

<real-password-only>
    <button>Change your password</button>
</real-password-only>
```

Tag helpers are registered automatically when the assembly is referenced from a Razor project.

## Blocking sensitive actions

The package registers the policy `MasterPass.RequireRealPassword`, which rejects master pass
sessions:

```csharp
app.MapPost("/account/change-password", ChangePassword)
   .RequireAuthorization(MasterPassDefaults.RequireRealPasswordPolicy);
```

```csharp
[Authorize(Policy = MasterPassDefaults.RequireRealPasswordPolicy)]
public IActionResult DeleteAccount() => View();
```

## Audit trail

Every attempt that reaches a decision is written to `IMasterPassAuditSink`. The default sink logs
grants at `Warning` level so they stand out in production logs. Replace it to write to your own
audit table:

```csharp
builder.Services.AddSingleton<IMasterPassAuditSink, DatabaseAuditSink>();
```

```csharp
public sealed class DatabaseAuditSink : IMasterPassAuditSink
{
    public void Write(MasterPassAuditEvent auditEvent)
    {
        // auditEvent.Outcome, User, RemoteIpAddress, Timestamp, Reason
        // Never store the password: it is not part of the event.
    }
}
```

## Secret from somewhere else

The default provider reads configuration. Replace it when the secret lives in a database or a vault:

```csharp
public sealed class VaultMasterPassSecretProvider : IMasterPassSecretProvider
{
    public string? GetMasterPassword(MasterPassAttemptContext attempt) =>
        _vault.GetCurrentSecret();
}

builder.Services.AddSingleton<IMasterPassSecretProvider, VaultMasterPassSecretProvider>();
```

`GetMasterPassword` is synchronous because `IPasswordHasher<TUser>` is synchronous. Cache the lookup.

## How it works

`AddMasterPass<TUser>` decorates two services that Identity already registered:

1. `IPasswordHasher<TUser>` verifies the account's own password first. Only when that fails does it
   compare the supplied password against the master secret in constant time (or verify it as an
   Identity hash), then applies the environment guard and `AllowForUser`.
2. `IUserClaimsPrincipalFactory<TUser>` adds the `masterpass` claim to the ticket when the current
   request was verified with the master password.

Nothing is stored in your database, and no password hash is changed.

## Security notes

- Treat the master password as a break-glass credential: long, random, rotated, kept in a secret
  manager, never in `appsettings.json` committed to source control.
- `AllowInProduction` defaults to `false` on purpose. Startup fails loudly rather than shipping a
  working backdoor.
- Lockout, two-factor and confirmed-account rules still apply. The master password proves the
  password factor only.
- Grants are audited through `IMasterPassAuditSink`; wire it to durable storage before enabling in
  production.

## Limitations

- Identity-based cookie authentication only. JWT-only setups are not covered.
- The master password does not bypass two-factor authentication or account lockout.
- Password rehash on successful sign-in is skipped for master password sessions, so a master pass
  login never rewrites the user's real hash.

## Sample

```bash
dotnet run --project samples/AspNet.MasterPass.Sample
```

The sample seeds `demo` / `Demo#12345` and `admin` / `Admin#12345`, with the master password
`local-dev-master` in `appsettings.Development.json`. The `admin` account is excluded through
`AllowForUser`.

## Development

```bash
dotnet build -c Release
dotnet test tests/AspNet.MasterPass.Tests -c Release
dotnet pack src/AspNet.MasterPass -c Release -o artifacts
```

Tag a release with `git tag v0.1.1 && git push origin v0.1.1`. The publish workflow authenticates to
nuget.org with Trusted Publishing (GitHub OIDC), using the `NUGET_USER` repository secret that holds
the nuget.org username.

## License

MIT. See [LICENSE](LICENSE).