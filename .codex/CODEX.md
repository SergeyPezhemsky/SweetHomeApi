# SweetHomeApi Codex Context

## Purpose

This repository is a home system project with an ASP.NET Core API, PostgreSQL persistence, ASP.NET Identity authentication, health tracking, and user widgets.

## Repository Map

- `SweetHomeApi/` - ASP.NET Core entry point, controllers, dependency registration, app settings.
- `Application/` - application/domain modules for widgets and health.
- `Persistance/` - EF Core DbContext, repositories, migrations. The folder name is intentionally spelled `Persistance` in the current project.
- `_deploy/` - SweetHome deployment configs.
- `openspec/` - OpenSpec source-of-truth specs and proposed changes.
- `.codex/skills/openspec-*` - OpenSpec Codex skills generated for the project.

## .NET Application

- Target framework: `.NET 9`.
- Main entry point: `SweetHomeApi/Program.cs`.
- Database: PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Auth: ASP.NET Core Identity with cookie authentication.
- API modules:
  - `api/Account/*` - login, register, current user.
  - `api/Widgets` - authenticated user widgets.
  - `api/Health` - authenticated health day data.
- EF migrations are in `Persistance/Migrations`.
- Runtime migration is currently called during app startup via `context.Database.Migrate()`.

## Verification Notes

- `dotnet build SweetHomeApi.sln --no-restore` succeeded on 2026-06-03 with 0 warnings and 0 errors.
- `npm test` could not run because `npm` was not available in the current shell.
- Direct bundled `node.exe --test` also failed with `Access denied`.
- `git status` failed because Git marked the repository as dubious ownership. Fix by adding the current workspace path to Git `safe.directory` if Git operations are needed.
- OpenSpec is installed as `@fission-ai/openspec` version `1.4.1` under `%APPDATA%/npm/node_modules/@fission-ai/openspec`. The current Codex shell still cannot run `openspec.cmd` because it resolves to an unavailable WindowsApps `node.exe`, but project OpenSpec skills exist under `.codex/skills`.

## Current Risks

- Removed deployment secrets and generated packages from the workspace on 2026-06-03. If `_deploy/id_rsa`, `_deploy/family_pad1.ovpn`, or related files were ever shared or pushed, rotate credentials.
- Swagger is enabled unconditionally in `SweetHomeApi/Program.cs`.
- Database migrations run automatically during app startup.
- Health numeric values are stored as strings, which limits validation and analytics.
- Health dictionary state is stored as serialized JSON text in `DictionaryStateJson`; this is acceptable for MVP but weak for querying/analytics.

## Working Guidelines

- Prefer existing layering and naming unless a task explicitly calls for refactoring.
- Do not rename `Persistance` casually; it affects project references, namespaces, migrations, and build paths.
- Keep API changes compatible with the likely frontend contract unless the user asks for a breaking change.
- Before edits, check for user changes and avoid reverting unrelated work.
- Use focused tests/builds after changes:
  - `.NET`: `dotnet build SweetHomeApi.sln --no-restore`
