# Proposal: Remove Unrelated Files

## Intent

Keep the repository directory focused on the SweetHome system by deleting files that are not part of this project.

## Scope

- Remove the standalone Jira preview Node application from the project directory.
- Remove Jira preview deployment helpers from the project directory.
- Remove local SSH key material from the project directory.
- Keep SweetHome API, application, persistence, deployment config, OpenSpec, and Codex context.

## Non-Goals

- Do not rewrite Git history.
- Do not delete SweetHome deployment scripts.
- Do not modify unrelated API changes already present in the working tree.

