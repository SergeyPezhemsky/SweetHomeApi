## ADDED Requirements

### Requirement: Home Assistant action execution
The system SHALL allow an authenticated user to execute supported Home Assistant actions through smart-home widgets.

#### Scenario: Execute a supported action
- GIVEN the client has a valid authenticated session
- AND the request contains a supported `entityId` domain and `action`
- WHEN the client requests `POST /api/SmartHome/actions`
- THEN the system calls the mapped Home Assistant service with the target `entity_id`
- AND the system returns `204 No Content` after Home Assistant accepts the call.

#### Scenario: Reject unsupported action
- GIVEN the client has a valid authenticated session
- AND the request contains an unsupported domain or action
- WHEN the client requests `POST /api/SmartHome/actions`
- THEN the system returns `400 Bad Request`
- AND Home Assistant is not called.

### Requirement: Cover position control
The system SHALL expose and execute percentage-based position control for Home Assistant cover entities that support position reporting.

#### Scenario: Catalog includes cover position slider
- GIVEN a Home Assistant `cover` entity has a `current_position` attribute
- WHEN the client requests `GET /api/SmartHome/widget-catalog`
- THEN the catalog widget contains `position` in `capabilities`
- AND the catalog widget contains a `slider` control with `action` equal to `position`, `min` equal to `0`, `max` equal to `100`, `step` equal to `1`, and `unit` equal to `%`.

#### Scenario: Set cover opening percentage
- GIVEN the client has a valid authenticated session
- AND the request body contains `entityId` equal to a `cover` entity, `action` equal to `position`, and `value` between `0` and `100`
- WHEN the client requests `POST /api/SmartHome/actions`
- THEN the system calls Home Assistant service `cover.set_cover_position`
- AND the service data contains `entity_id` and `position`.

#### Scenario: Reject invalid cover percentage
- GIVEN the client has a valid authenticated session
- AND the request body contains `action` equal to `position`
- AND `value` is missing, not numeric, less than `0`, or greater than `100`
- WHEN the client requests `POST /api/SmartHome/actions`
- THEN the system returns `400 Bad Request`
- AND Home Assistant is not called.
