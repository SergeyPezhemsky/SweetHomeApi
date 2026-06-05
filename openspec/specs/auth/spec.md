# Auth Specification

## Requirements

### Requirement: User Registration
The system SHALL allow a new user to register with a name, email, and password.

#### Scenario: Successful registration
- GIVEN no existing user has the submitted email
- WHEN the client submits valid registration data to `POST /api/Account/Register`
- THEN the system creates an ASP.NET Identity user
- AND creates the default widget set for that user
- AND signs the user in with a cookie session
- AND returns a success response.

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
- WHEN the client submits credentials to `POST /api/Account/Login`
- THEN the system creates an authenticated cookie session
- AND returns the user's display name.

#### Scenario: Invalid login
- GIVEN the submitted email is unknown or the password is invalid
- WHEN the client submits credentials to `POST /api/Account/Login`
- THEN the system returns an unauthorized response.

### Requirement: Current User
The system SHALL expose the current authenticated user's name.

#### Scenario: Authenticated current user request
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Account/User`
- THEN the system returns the current user's name.

#### Scenario: Anonymous current user request
- GIVEN the client has no valid authenticated session
- WHEN the client requests `GET /api/Account/User`
- THEN the system returns an unauthorized response.

