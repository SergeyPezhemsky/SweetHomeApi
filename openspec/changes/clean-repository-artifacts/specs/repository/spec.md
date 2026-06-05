# Delta for Repository Hygiene

## ADDED Requirements

### Requirement: Deployment Secrets Excluded
The repository SHALL exclude private deployment secrets and VPN configuration files from version control.

#### Scenario: Private key present locally
- GIVEN a private key exists under `_deploy/`
- WHEN Git evaluates ignored files
- THEN the private key is ignored
- AND it is not offered as an untracked file for commit.

#### Scenario: VPN profile present locally
- GIVEN an `.ovpn` profile exists under `_deploy/`
- WHEN Git evaluates ignored files
- THEN the VPN profile is ignored
- AND it is not offered as an untracked file for commit.

### Requirement: Generated Deployment Artifacts Excluded
The repository SHALL exclude generated deployment packages and publish output from version control.

#### Scenario: Deployment archive generated
- GIVEN a `.zip` package exists under `_deploy/`
- WHEN Git evaluates ignored files
- THEN the archive is ignored
- AND it is not offered as an untracked file for commit.

#### Scenario: Publish output generated
- GIVEN publish output exists under `_deploy/publish/`
- WHEN Git evaluates ignored files
- THEN the publish output is ignored
- AND it is not offered as an untracked file for commit.

### Requirement: Deployment Source Files Preserved
The repository SHALL keep reusable deployment source/configuration files versionable.

#### Scenario: Deploy config exists
- GIVEN `_deploy/sweethome-api.nginx.conf` exists
- WHEN Git evaluates untracked files
- THEN the file remains eligible to be committed.
