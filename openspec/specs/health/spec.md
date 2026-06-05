# Health Specification

## Requirements

### Requirement: Health Day Retrieval
The system SHALL return health configuration and health data for an authenticated user and date.

#### Scenario: Fetch existing day
- GIVEN the client has a valid authenticated session
- AND the user has a health entry for the requested date
- WHEN the client requests `GET /api/Health?date=<date>`
- THEN the system returns the default health sections
- AND returns the stored health values for that date.

#### Scenario: Fetch empty day
- GIVEN the client has a valid authenticated session
- AND the user has no health entry for the requested date
- WHEN the client requests `GET /api/Health?date=<date>`
- THEN the system returns the default health sections
- AND returns an empty day model with default dictionary values.

#### Scenario: Invalid date
- GIVEN the requested date is not in `dd.MM.yyyy` or `yyyy-MM-dd` format
- WHEN the client requests `GET /api/Health?date=<date>`
- THEN the system returns a bad request response.

### Requirement: Health Day Update
The system SHALL upsert health data for an authenticated user and date.

#### Scenario: Create health day
- GIVEN the client has a valid authenticated session
- AND no health entry exists for the submitted date
- WHEN the client submits data to `PUT /api/Health`
- THEN the system creates a health entry for the current user and date
- AND returns the updated day response.

#### Scenario: Partially update health day
- GIVEN the client has a valid authenticated session
- AND a health entry exists for the submitted date
- WHEN the client submits data to `PUT /api/Health` with only some fields set
- THEN the system updates submitted fields
- AND preserves fields omitted from the request.

### Requirement: Health Dictionary State
The system SHALL return the configured health dictionary with per-day boolean values.

#### Scenario: Dictionary merge
- GIVEN the default dictionary contains a configured item
- AND the stored health entry contains a boolean value for that item ID
- WHEN the system builds the health day response
- THEN the response includes the configured item with the stored value.

#### Scenario: Unknown or missing dictionary values
- GIVEN the stored dictionary state is missing an item ID or cannot be parsed
- WHEN the system builds the health day response
- THEN the response falls back to the default false value for that item.

