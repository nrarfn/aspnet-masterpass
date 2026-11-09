# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

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