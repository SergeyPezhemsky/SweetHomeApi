# Tasks

## 1. Remove Unrelated Files

- [x] 1.1 Delete `_external/`.
- [x] 1.2 Delete `_local/`.
- [x] 1.3 Ensure root `node/` is absent.
- [x] 1.4 Ensure Jira preview deploy helpers are absent from `_deploy/` and `scripts/`.

## 2. Update References

- [x] 2.1 Point backend deploy script to an SSH key outside the repository by default.
- [x] 2.2 Remove stale Jira preview context from Codex notes.
- [x] 2.3 Remove stale `_external/` and `_local/` notes.

## 3. Verification

- [x] 3.1 Search for stale references.
- [x] 3.2 Run `dotnet build SweetHomeApi.sln --no-restore`.
- [x] 3.3 Check Git status.
