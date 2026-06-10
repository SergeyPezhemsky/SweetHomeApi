## ADDED Requirements

### Requirement: Smart-home layout retrieval
The system SHALL return the authenticated user's persisted smart-home rooms and widgets.

#### Scenario: Fetch user layout
- GIVEN an authenticated user has saved smart-home rooms and widgets
- WHEN the client requests `GET /api/SmartHome/layout`
- THEN the response contains only rooms and widgets owned by that user.

### Requirement: Smart-home layout persistence
The system SHALL allow an authenticated user to replace their smart-home rooms and selected widgets.

#### Scenario: Replace user layout
- GIVEN an authenticated user submits rooms and widgets to `PUT /api/SmartHome/layout`
- WHEN the request is valid
- THEN the system stores those rooms and widgets for the current user
- AND widgets can be bound to submitted rooms by `RoomId`.
