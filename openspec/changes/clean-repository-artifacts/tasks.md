# Tasks

## 1. Ignore Rules

- [x] 1.1 Add deployment secret and artifact patterns to `.gitignore`.

## 2. Cleanup

- [x] 2.1 Remove `_deploy/id_rsa`.
- [x] 2.2 Remove `_deploy/*.ovpn`.
- [x] 2.3 Remove `_deploy/*.zip`.
- [x] 2.4 Remove `_deploy/publish/`.
- [x] 2.5 Remove root `openvpn` if it is a local VPN-related file.
- [x] 2.6 Remove local SSH key from the project directory.

## 3. Verification

- [x] 3.1 Check Git status.
- [x] 3.2 Run `dotnet build SweetHomeApi.sln --no-restore`.
