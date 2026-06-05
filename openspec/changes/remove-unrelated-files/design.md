# Design: Remove Unrelated Files

## Approach

Delete unrelated application/tool files rather than moving them under a project-local holding folder. Local secrets should live outside the repository, for example under the user's home `.ssh` directory.

## Removed Paths

- `_external/`
- `_local/`
- root `node/` directory, already absent after cleanup
- Jira preview deploy helpers, already absent from `_deploy/` and `scripts/`

## Deploy Key Path

`scripts/deploy-backend.ps1` defaults to `$HOME\.ssh\id_ed25519` so deploy credentials can live outside the project directory.

## Verification

- Confirm removed paths do not exist.
- Search project context for stale `_external`, `_local`, and Jira preview references.
- Build the .NET solution.

