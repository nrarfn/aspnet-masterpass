# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.1] - 2026-11-10

### Changed

- Packaging metadata (readme, icon, package validation) moved from the repository-wide
  `Directory.Build.props` to the library project, and `PackageId` is set explicitly.
- `IsPackable` is now `false` by default for every project, so only the library can be packed.

### Documentation

- Added Indonesian (`README.id.md`) and Simplified Chinese (`README.zh-CN.md`) translations.

## [0.1.0] - 2026-11-09

### Added

- `AddMasterPass<TUser>` registration that decorates ASP.NET Core Identity's password hasher and
  claims principal factory.
- Master password as plaintext or as an ASP.NET Core Identity password hash.
- `MasterPassOptions` with environment guard, per-user `AllowForUser` gate and configurable options.
- `masterpass` claim, `User.IsMasterPassLogin()` and the `<master-pass-only>` /
  `<real-password-only>` tag helpers.
- `MasterPass.RequireRealPassword` authorization policy for actions that must not run in a master
  pass session.
- `IMasterPassSecretProvider` for secrets kept outside configuration, and `IMasterPassAuditSink`
  for durable audit logging.
- Sample application and integration test suite covering real password, master password, hashed
  secret, admin exclusion, production guard and the policy.