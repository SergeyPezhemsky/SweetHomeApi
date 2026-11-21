# Auth Specification

## Requirements

### Requirement: User Registration
The system SHALL allow a new user to register with a name, email, and password.

#### Scenario: Successful registration
- GIVEN no existing user has the submitted email
- WHEN the client submits valid registration data to `POST /api/Account/Register`
- THEN the system creates a user with the Pending approval status and the admin flag set to false
- AND creates the default widget set for that user
- AND returns a success response without issuing a session cookie.

#### Scenario: Duplicate email
- GIVEN a user already exists with the submitted email
- WHEN the client submits registration data to `POST /api/Account/Register`
- THEN the system rejects the request
- AND returns a bad request response.

### Requirement: User Login
The system SHALL allow an existing user to authenticate with email and password.

#### Scenario: Successful login
- GIVEN a user exists with the submitted email
- AND the submitted password is valid
- AND the account is approved
- WHEN the client submits credentials to `POST /api/Account/Login`
- THEN the system creates an authenticated cookie session
- AND returns the user's display name.

#### Scenario: Invalid login
- GIVEN the submitted email is unknown or the password is invalid
- WHEN the client submits credentials to `POST /api/Account/Login`
- THEN the system returns an unauthorized response.

#### Scenario: Login before approval or after rejection
- GIVEN the submitted credentials are valid
- AND the account is pending or rejected
- WHEN the client submits credentials to `POST /api/Account/Login`
- THEN the system returns 403 with a message for the account's approval status
- AND does not issue a session cookie.

#### Scenario: Existing accounts after migration
- GIVEN a user was registered before the approval workflow was introduced
- WHEN the database migration runs
- THEN the account becomes approved and the admin flag is set to false.

### Requirement: Current User
The system SHALL expose the current authenticated user's name, email, and admin flag.

#### Scenario: Authenticated current user request
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Account/User`
- THEN the system returns the current user's name, email, and admin flag.

#### Scenario: Anonymous current user request
- GIVEN the client has no valid authenticated session
- WHEN the client requests `GET /api/Account/User`
- THEN the system returns an unauthorized response.

### Requirement: Registration Requests
The system SHALL allow an approved administrator to review pending registrations from the profile.

#### Scenario: View pending requests
- GIVEN a signed-in administrator
- WHEN the client requests `GET /api/Account/RegistrationRequests`
- THEN the system returns pending requests with the account ID, name, email, and registration time.

#### Scenario: Decide a request
- GIVEN a signed-in administrator and a pending request
- WHEN the client sends `PUT /api/Account/RegistrationRequests/{userId}` with status `Approved` or `Rejected`
- THEN the system saves the decision and removes the request from the pending list
- AND returns 204.

#### Scenario: Repeat a decision
- GIVEN the request has already been reviewed
- WHEN an administrator submits the same decision again
- THEN the system returns 204.

#### Scenario: Conflicting decision
- GIVEN the request has already been reviewed
- WHEN an administrator submits a different decision
- THEN the system returns 409 and preserves the original decision.

#### Scenario: Review without administrator privileges
- GIVEN a signed-in user without the admin flag
- WHEN the client requests the pending list or submits a decision
- THEN the system returns 403.

