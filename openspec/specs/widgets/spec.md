# Widgets Specification

## Requirements

### Requirement: User Widget List
The system SHALL return only widgets owned by the authenticated user.

#### Scenario: Fetch widgets
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Widgets`
- THEN the system returns the widgets whose `UserId` matches the current user.

#### Scenario: Anonymous fetch
- GIVEN the client has no valid authenticated session
- WHEN the client requests `GET /api/Widgets`
- THEN the system returns an unauthorized response.

### Requirement: Widget Updates
The system SHALL allow an authenticated user to update their widget layout and visibility.

#### Scenario: Update existing widgets
- GIVEN the client has a valid authenticated session
- AND the submitted widget IDs belong to the current user
- WHEN the client submits a widget list to `PUT /api/Widgets`
- THEN the system updates alias, order, name, icon, size, and hide state for matching widgets
- AND ignores widgets that do not belong to the current user.

#### Scenario: Empty update
- GIVEN the client has a valid authenticated session
- WHEN the client submits an empty widget list to `PUT /api/Widgets`
- THEN the system returns no content
- AND makes no widget changes.

### Requirement: Default Widgets
The system SHALL create a default widget set for each newly registered user.

#### Scenario: Defaults after registration
- GIVEN a user registration succeeds
- WHEN default widgets are created
- THEN the system creates widgets for home, movies, books, trips, coins, and health.

