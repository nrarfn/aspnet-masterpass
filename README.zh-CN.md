# AspNet.MasterPass

[English](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.md) ·
[Bahasa Indonesia](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.id.md) ·
[中文](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/AspNet.MasterPass.svg)](https://www.nuget.org/packages/AspNet.MasterPass)
[![NuGet downloads](https://img.shields.io/nuget/dt/AspNet.MasterPass.svg)](https://www.nuget.org/packages/AspNet.MasterPass)
[![ci](https://github.com/nrarfn/aspnet-masterpass/actions/workflows/ci.yml/badge.svg)](https://github.com/nrarfn/aspnet-masterpass/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

一个主密码，登录任意 ASP.NET Core Identity 账户。

为两种场景而建：支持工程师或开发者确实需要以另一个用户身份操作——复现只在该账户出现的问题，
以及在不索取密码的前提下帮助被锁定的用户。每个账户保留自己的常规密码；主密码是第二个有效
密码，且只在允许它的地方生效。

灵感来自 [laravel-MasterPass](https://github.com/imanghafoori1/laravel-MasterPass)，为
ASP.NET Core Identity 重写。

## 环境要求

- ASP.NET Core Identity（`AddIdentity`、`AddDefaultIdentity`，或 `AddIdentityCore` + `AddSignInManager`）
- Cookie 认证，Identity 已为你配置好
- .NET 8、.NET 9 或 .NET 10

## 安装

```bash
dotnet add package AspNet.MasterPass
```

在 Identity **之后**注册该功能：

```csharp
using AspNet.MasterPass;

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMasterPass<IdentityUser>(builder.Configuration);
```

然后提供密钥。它可以是明文密码，也可以是 ASP.NET Core Identity 的密码哈希：

```json
{
  "MasterPass": {
    "Password": "a-long-and-boring-master-password"
  }
}
```

```bash
# 或者在容器中更推荐这样
MasterPass__Password=a-long-and-boring-master-password
```

没有密钥时该包完全不生效：装饰器直接把调用转交给默认的 Identity 密码哈希器。

## 防护栏

生产环境默认被阻止。如果在宿主环境为 `Production` 时配置了主密码，应用将拒绝启动。确实需要
时才显式开启：

```csharp
builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = builder.Configuration["MasterPass:Password"];
    options.AllowInProduction = true;
});
```

让管理账户继续使用自己的密码，这正是 `AllowForUser` 的用途：

```csharp
builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = builder.Configuration["MasterPass:Password"];

    options.AllowForUser = attempt =>
        attempt.User is IdentityUser user &&
        !user.Email!.EndsWith("@yourcompany.com", StringComparison.OrdinalIgnoreCase);
});
```

`AllowForUser` 在主密码已经匹配之后运行，因此它是策略闸门，而不是第二道密码校验。

## 识别使用主密码的会话

当会话由主密码发起时，认证票据会带有 `masterpass` claim：

```csharp
if (User.IsMasterPassLogin())
{
    // 显示警告横幅、隐藏破坏性操作……
}
```

```html
<master-pass-only>
    <p>你正以其他用户身份登录。所有操作都会被记录。</p>
</master-pass-only>

<real-password-only>
    <button>修改密码</button>
</real-password-only>
```

在 Razor 项目引用该程序集时，标签帮助器会自动注册。

## 阻止敏感操作

该包注册了策略 `MasterPass.RequireRealPassword`，用于拒绝主密码会话：

```csharp
app.MapPost("/account/change-password", ChangePassword)
   .RequireAuthorization(MasterPassDefaults.RequireRealPasswordPolicy);
```

```csharp
[Authorize(Policy = MasterPassDefaults.RequireRealPasswordPolicy)]
public IActionResult DeleteAccount() => View();
```

## 审计日志

每一次形成判定的尝试都会写入 `IMasterPassAuditSink`。默认实现以 `Warning` 级别记录授权，便于在
生产日志中突出显示。替换它即可写入你自己的审计表：

```csharp
builder.Services.AddSingleton<IMasterPassAuditSink, DatabaseAuditSink>();
```

```csharp
public sealed class DatabaseAuditSink : IMasterPassAuditSink
{
    public void Write(MasterPassAuditEvent auditEvent)
    {
        // auditEvent.Outcome, User, RemoteIpAddress, Timestamp, Reason
        // 永远不要存储密码：事件中并不包含它。
    }
}
```

## 从其他来源获取密钥

默认提供程序读取配置。当密钥存放在数据库或 vault 中时替换它：

```csharp
public sealed class VaultMasterPassSecretProvider : IMasterPassSecretProvider
{
    public string? GetMasterPassword(MasterPassAttemptContext attempt) =>
        _vault.GetCurrentSecret();
}

builder.Services.AddSingleton<IMasterPassSecretProvider, VaultMasterPassSecretProvider>();
```

`GetMasterPassword` 是同步的，因为 `IPasswordHasher<TUser>` 本身是同步的。请缓存查询结果。

## 工作原理

`AddMasterPass<TUser>` 装饰 Identity 已经注册的两个服务：

1. `IPasswordHasher<TUser>` 先校验账户自己的密码。只有当校验失败时，才以恒定时间把提交的密码
   与主密钥比较（或将其作为 Identity 哈希校验），随后应用环境防护与 `AllowForUser`。
2. 当当前请求是通过主密码验证的，`IUserClaimsPrincipalFactory<TUser>` 会把 `masterpass` claim
   加入票据。

不会在你的数据库中存储任何内容，也不会修改任何密码哈希。

## 安全说明

- 把主密码当作 break-glass 凭据：足够长、随机生成、定期轮换、保存在密钥管理器中，绝不放在
  提交到源码管理的 `appsettings.json` 里。
- `AllowInProduction` 故意默认为 `false`。宁可启动时明确失败，也不发布一个可用的后门。
- 锁定、双因素和已确认账户等规则依然生效。主密码只能证明密码这一因素。
- 授权通过 `IMasterPassAuditSink` 审计；在生产启用前请接入持久化存储。

## 限制

- 仅支持基于 Identity 的 Cookie 认证。纯 JWT 方案不在覆盖范围内。
- 主密码不会绕过双因素认证或账户锁定。
- 使用主密码登录成功时会跳过密码重新哈希，因此主密码登录永远不会重写用户的真实哈希。

## 示例

```bash
dotnet run --project samples/AspNet.MasterPass.Sample
```

示例预置了 `demo` / `Demo#12345` 与 `admin` / `Admin#12345`，主密码为 `local-dev-master`，位于
`appsettings.Development.json`。`admin` 账户通过 `AllowForUser` 排除。

## 开发

```bash
dotnet build -c Release
dotnet test tests/AspNet.MasterPass.Tests -c Release
dotnet pack src/AspNet.MasterPass -c Release -o artifacts
```

用 `git tag v0.1.1 && git push origin v0.1.1` 标记发布。发布工作流通过 Trusted Publishing
（GitHub OIDC）向 nuget.org 认证，使用存有 nuget.org 用户名的仓库 secret `NUGET_USER`。

## 许可证

MIT，见 [LICENSE](LICENSE)。