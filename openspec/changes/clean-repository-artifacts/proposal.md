# Proposal: Clean Repository Artifacts

## Intent

Remove secrets and generated deployment artifacts from the repository workspace and prevent them from being added again.

## Scope

- Ignore private keys, VPN profiles, packaged deployment archives, and publish output under `_deploy/`.
- Remove existing sensitive/generated files from the working tree or Git index where present.
- Preserve SweetHome deploy source files that are useful to version:
  - `_deploy/sweethome-api.nginx.conf`

## Non-Goals

- Do not change application behavior.
- Do not rotate credentials automatically.
- Do not rewrite Git history.
- Do not modify unrelated API files already changed in the working tree.

## Risks

- Files such as `_deploy/id_rsa` and `_deploy/family_pad1.ovpn` should be considered exposed if the repository was shared or pushed after they were added.
- Removing local deployment archives means future deploys must regenerate them from source.
