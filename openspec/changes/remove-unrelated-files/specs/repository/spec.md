# Delta for Repository Organization

## ADDED Requirements

### Requirement: Repository Contains Only SweetHome Project Files
The repository directory SHALL contain SweetHome system files and project support files only.

#### Scenario: Unrelated Jira preview tool
- GIVEN the standalone Jira preview tool is not part of the SweetHome system
- WHEN the repository directory is cleaned
- THEN the Jira preview tool files are absent from the repository directory.

#### Scenario: Local SSH key material
- GIVEN local SSH key material is not project source
- WHEN the repository directory is cleaned
- THEN local SSH key files are absent from the repository directory
- AND deploy scripts reference an external default key path.

