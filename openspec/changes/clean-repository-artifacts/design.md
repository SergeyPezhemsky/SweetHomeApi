# Design: Clean Repository Artifacts

## Approach

Use `.gitignore` to block the known sensitive and generated deployment patterns, then remove existing matching files from the working tree. Keep reusable deployment scripts and service/nginx configuration files.

## Files to Ignore

- `_deploy/*.zip`
- `_deploy/publish/`
- `_deploy/id_rsa`
- `_deploy/*.ovpn`
- `openvpn`

## Files to Keep

- `_deploy/sweethome-api.nginx.conf`

## Verification

- Check Git status using `git -c safe.directory=... status --short --branch`.
- Confirm sensitive/generated files no longer appear as tracked or untracked candidates.
- Run `.NET` build because `.gitignore` and file deletion should not affect compilation, but the repository currently has unrelated API changes.
