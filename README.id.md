# AspNet.MasterPass

[English](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.md) ·
[Bahasa Indonesia](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.id.md) ·
[中文](https://github.com/nrarfn/aspnet-masterpass/blob/main/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/AspNet.MasterPass.svg)](https://www.nuget.org/packages/AspNet.MasterPass)
[![NuGet downloads](https://img.shields.io/nuget/dt/AspNet.MasterPass.svg)](https://www.nuget.org/packages/AspNet.MasterPass)
[![ci](https://github.com/nrarfn/aspnet-masterpass/actions/workflows/ci.yml/badge.svg)](https://github.com/nrarfn/aspnet-masterpass/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Satu master password untuk masuk ke akun ASP.NET Core Identity mana pun.

Dibuat untuk dua situasi ketika support engineer atau developer benar-benar perlu menjadi pengguna
lain: mereproduksi bug yang hanya muncul di akun tersebut, dan membantu pengguna yang terkunci tanpa
menanyakan kata sandinya. Setiap akun tetap memakai kata sandi normalnya; master password adalah kata
sandi kedua yang valid dan hanya berlaku di tempat yang kamu izinkan.

Diadaptasi dari ide [laravel-MasterPass](https://github.com/imanghafoori1/laravel-MasterPass), lalu
dibangun ulang untuk ASP.NET Core Identity.

## Kebutuhan

- ASP.NET Core Identity (`AddIdentity`, `AddDefaultIdentity`, atau `AddIdentityCore` + `AddSignInManager`)
- Autentikasi cookie, yang sudah disiapkan Identity untukmu
- .NET 8, .NET 9, atau .NET 10

## Instalasi

```bash
dotnet add package AspNet.MasterPass
```

Daftarkan fitur ini **setelah** Identity:

```csharp
using AspNet.MasterPass;

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddMasterPass<IdentityUser>(builder.Configuration);
```

Lalu sediakan secret-nya. Bisa berupa kata sandi plaintext atau hash kata sandi ASP.NET Core Identity:

```json
{
  "MasterPass": {
    "Password": "a-long-and-boring-master-password"
  }
}
```

```bash
# atau, lebih disarankan di container
MasterPass__Password=a-long-and-boring-master-password
```

Tanpa secret, paket ini tidak melakukan apa pun: decorator langsung meneruskan ke password hasher
Identity bawaan.

## Pengaman

Produksi diblokir secara default. Jika master password dikonfigurasi sementara environment host adalah
`Production`, aplikasi menolak start. Aktifkan hanya kalau kamu benar-benar berniat:

```csharp
builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = builder.Configuration["MasterPass:Password"];
    options.AllowInProduction = true;
});
```

Biarkan akun administratif memakai kata sandinya sendiri. Untuk itu ada `AllowForUser`:

```csharp
builder.Services.AddMasterPass<IdentityUser>(options =>
{
    options.Password = builder.Configuration["MasterPass:Password"];

    options.AllowForUser = attempt =>
        attempt.User is IdentityUser user &&
        !user.Email!.EndsWith("@yourcompany.com", StringComparison.OrdinalIgnoreCase);
});
```

`AllowForUser` berjalan setelah master password cocok, jadi ini gerbang kebijakan, bukan pemeriksaan
kata sandi kedua.

## Mengetahui sesi mana yang memakainya

Tiket autentikasi membawa claim `masterpass` ketika sesi dimulai dengan master password:

```csharp
if (User.IsMasterPassLogin())
{
    // tampilkan banner peringatan, sembunyikan aksi destruktif, ...
}
```

```html
<master-pass-only>
    <p>Kamu masuk sebagai pengguna lain. Semua aksi dicatat.</p>
</master-pass-only>

<real-password-only>
    <button>Ubah kata sandi</button>
</real-password-only>
```

Tag helper terdaftar otomatis saat assembly direferensikan dari proyek Razor.

## Memblokir aksi sensitif

Paket mendaftarkan policy `MasterPass.RequireRealPassword`, yang menolak sesi master pass:

```csharp
app.MapPost("/account/change-password", ChangePassword)
   .RequireAuthorization(MasterPassDefaults.RequireRealPasswordPolicy);
```

```csharp
[Authorize(Policy = MasterPassDefaults.RequireRealPasswordPolicy)]
public IActionResult DeleteAccount() => View();
```

## Jejak audit

Setiap percobaan yang sampai pada sebuah keputusan ditulis ke `IMasterPassAuditSink`. Sink bawaan
mencatat pemberian akses pada level `Warning` supaya menonjol di log produksi. Ganti untuk menulis ke
tabel auditmu sendiri:

```csharp
builder.Services.AddSingleton<IMasterPassAuditSink, DatabaseAuditSink>();
```

```csharp
public sealed class DatabaseAuditSink : IMasterPassAuditSink
{
    public void Write(MasterPassAuditEvent auditEvent)
    {
        // auditEvent.Outcome, User, RemoteIpAddress, Timestamp, Reason
        // Jangan pernah menyimpan kata sandinya: nilainya tidak ada di event.
    }
}
```

## Secret dari sumber lain

Provider bawaan membaca konfigurasi. Ganti ketika secret berada di database atau vault:

```csharp
public sealed class VaultMasterPassSecretProvider : IMasterPassSecretProvider
{
    public string? GetMasterPassword(MasterPassAttemptContext attempt) =>
        _vault.GetCurrentSecret();
}

builder.Services.AddSingleton<IMasterPassSecretProvider, VaultMasterPassSecretProvider>();
```

`GetMasterPassword` sinkron karena `IPasswordHasher<TUser>` juga sinkron. Cache hasil pembacaannya.

## Cara kerjanya

`AddMasterPass<TUser>` mendekorasi dua service yang sudah didaftarkan Identity:

1. `IPasswordHasher<TUser>` memverifikasi kata sandi milik akun terlebih dahulu. Hanya ketika gagal,
   ia membandingkan kata sandi yang diberikan dengan master secret secara constant time (atau
   memverifikasinya sebagai hash Identity), lalu menerapkan guard environment dan `AllowForUser`.
2. `IUserClaimsPrincipalFactory<TUser>` menambahkan claim `masterpass` ke tiket ketika request saat
   ini terverifikasi dengan master password.

Tidak ada yang disimpan di database-mu, dan tidak ada hash kata sandi yang diubah.

## Catatan keamanan

- Perlakukan master password sebagai kredensial break-glass: panjang, acak, dirotasi, disimpan di
  secret manager, dan jangan pernah di `appsettings.json` yang ikut di-commit.
- `AllowInProduction` default `false` dengan sengaja. Startup gagal dengan jelas daripada mengirim
  backdoor yang berfungsi.
- Aturan lockout, two-factor, dan akun terkonfirmasi tetap berlaku. Master password hanya membuktikan
  faktor kata sandi.
- Pemberian akses diaudit lewat `IMasterPassAuditSink`; sambungkan ke penyimpanan permanen sebelum
  mengaktifkannya di produksi.

## Keterbatasan

- Hanya autentikasi cookie berbasis Identity. Setup yang hanya memakai JWT tidak tercakup.
- Master password tidak melewati two-factor authentication atau lockout akun.
- Rehash kata sandi saat sign-in berhasil dilewati untuk sesi master password, jadi login master pass
  tidak pernah menulis ulang hash asli pengguna.

## Contoh

```bash
dotnet run --project samples/AspNet.MasterPass.Sample
```

Contoh menyemai akun `demo` / `Demo#12345` dan `admin` / `Admin#12345`, dengan master password
`local-dev-master` di `appsettings.Development.json`. Akun `admin` dikecualikan lewat `AllowForUser`.

## Pengembangan

```bash
dotnet build -c Release
dotnet test tests/AspNet.MasterPass.Tests -c Release
dotnet pack src/AspNet.MasterPass -c Release -o artifacts
```

Tandai rilis dengan `git tag v0.1.1 && git push origin v0.1.1`. Workflow publish melakukan
autentikasi ke nuget.org dengan Trusted Publishing (GitHub OIDC), memakai secret repositori
`NUGET_USER` yang berisi username nuget.org.

## Lisensi

MIT. Lihat [LICENSE](LICENSE).