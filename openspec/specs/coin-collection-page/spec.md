# Страница для коллекции монет

## Purpose

Страница коллекции монет SHALL allow an authenticated user to view, search, create, edit, and organize their personal coin collection.

## Requirements

### Requirement: Coin Collection Page Load
The system SHALL return page data required to render the coin collection page for the authenticated user.

#### Scenario: Fetch collection page
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Coins`
- THEN the system returns only coins owned by the current user
- AND returns pagination metadata
- AND returns summary counters for the current filtered collection.

#### Scenario: Anonymous fetch
- GIVEN the client has no valid authenticated session
- WHEN the client requests `GET /api/Coins`
- THEN the system returns an unauthorized response.

### Requirement: Coin Search And Filters
The system SHALL allow filtering and sorting the collection without exposing another user's coins.

#### Scenario: Search by text
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Coins?search=<text>`
- THEN the system returns coins where name, country, series, mint, or notes match the search text.

#### Scenario: Filter by attributes
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Coins` with country, year, nominal, metal, condition, status, or tag filters
- THEN the system returns only current user's coins matching all submitted filters.

#### Scenario: Sort results
- GIVEN the client has a valid authenticated session
- WHEN the client requests `GET /api/Coins?sort=<field>&direction=<asc|desc>`
- THEN the system returns the matching coins sorted by the requested supported field.

### Requirement: Coin Details
The system SHALL return detailed information for one coin owned by the authenticated user.

#### Scenario: Fetch owned coin
- GIVEN the client has a valid authenticated session
- AND the requested coin belongs to the current user
- WHEN the client requests `GET /api/Coins/{coinId}`
- THEN the system returns the full coin details.

#### Scenario: Fetch missing or foreign coin
- GIVEN the client has a valid authenticated session
- AND the requested coin does not exist or belongs to another user
- WHEN the client requests `GET /api/Coins/{coinId}`
- THEN the system returns a not found response.

### Requirement: Coin Creation
The system SHALL allow an authenticated user to add a coin to their collection.

#### Scenario: Create coin
- GIVEN the client has a valid authenticated session
- WHEN the client submits coin data to `POST /api/Coins`
- THEN the system creates a coin owned by the current user
- AND returns the created coin details.

#### Scenario: Create invalid coin
- GIVEN the client has a valid authenticated session
- WHEN the client submits invalid coin data to `POST /api/Coins`
- THEN the system returns a validation error response
- AND makes no collection changes.

### Requirement: Coin Updates
The system SHALL allow an authenticated user to update only coins they own.

#### Scenario: Update coin
- GIVEN the client has a valid authenticated session
- AND the requested coin belongs to the current user
- WHEN the client submits data to `PUT /api/Coins/{coinId}`
- THEN the system updates submitted editable fields
- AND returns the updated coin details.

#### Scenario: Update missing or foreign coin
- GIVEN the client has a valid authenticated session
- AND the requested coin does not exist or belongs to another user
- WHEN the client submits data to `PUT /api/Coins/{coinId}`
- THEN the system returns a not found response.

### Requirement: Coin Deletion
The system SHALL allow an authenticated user to remove a coin from their collection.

#### Scenario: Delete coin
- GIVEN the client has a valid authenticated session
- AND the requested coin belongs to the current user
- WHEN the client requests `DELETE /api/Coins/{coinId}`
- THEN the system deletes the coin
- AND returns no content.

#### Scenario: Delete missing or foreign coin
- GIVEN the client has a valid authenticated session
- AND the requested coin does not exist or belongs to another user
- WHEN the client requests `DELETE /api/Coins/{coinId}`
- THEN the system returns a not found response.

## API Contract

All endpoints require the existing authenticated cookie session. Anonymous requests SHALL return `401 Unauthorized`.

### `GET /api/Coins`

Returns the current user's coin collection list.

#### Query Parameters

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `search` | string | no | Full-text search by name, country, series, mint, and notes. |
| `country` | string | no | Exact country filter. |
| `yearFrom` | integer | no | Minimum issue year. |
| `yearTo` | integer | no | Maximum issue year. |
| `nominal` | string | no | Coin face value, for example `1 рубль` or `50 euro cent`. |
| `metal` | string | no | Metal or alloy filter. |
| `condition` | string | no | Condition filter, for example `UNC`, `XF`, `VF`, `G`. |
| `status` | string | no | Collection status: `owned`, `wanted`, `sold`, or `duplicate`. |
| `tag` | string | no | Tag filter. Can be repeated. |
| `sort` | string | no | Supported values: `name`, `country`, `year`, `nominal`, `condition`, `purchaseDate`, `createdAt`, `updatedAt`. |
| `direction` | string | no | `asc` or `desc`. Defaults to `asc`. |
| `page` | integer | no | 1-based page number. Defaults to `1`. |
| `pageSize` | integer | no | Page size. Defaults to `50`, maximum `200`. |

#### Response `200 OK`

```json
{
  "items": [
    {
      "id": 12,
      "name": "1 рубль 1997",
      "country": "Россия",
      "year": 1997,
      "nominal": "1 рубль",
      "metal": "мельхиор",
      "condition": "VF",
      "status": "owned",
      "quantity": 1,
      "tags": ["Россия", "оборотные"],
      "imageUrl": "/api/Coins/12/image",
      "createdAt": "2026-06-03T18:00:00Z",
      "updatedAt": "2026-06-03T18:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1,
  "summary": {
    "ownedCount": 1,
    "wantedCount": 0,
    "duplicateCount": 0,
    "countriesCount": 1
  }
}
```

### `GET /api/Coins/{coinId}`

Returns full details for one coin.

#### Response `200 OK`

```json
{
  "id": 12,
  "name": "1 рубль 1997",
  "country": "Россия",
  "year": 1997,
  "nominal": "1 рубль",
  "series": "Регулярный выпуск",
  "mint": "ММД",
  "metal": "мельхиор",
  "diameterMm": 20.5,
  "weightGrams": 3.25,
  "condition": "VF",
  "status": "owned",
  "quantity": 1,
  "purchaseDate": "2026-06-03",
  "purchasePrice": 100.0,
  "currency": "RUB",
  "catalogNumber": "Y# 604",
  "tags": ["Россия", "оборотные"],
  "notes": "Монета из основной коллекции.",
  "imageUrl": "/api/Coins/12/image",
  "createdAt": "2026-06-03T18:00:00Z",
  "updatedAt": "2026-06-03T18:00:00Z"
}
```

### `POST /api/Coins`

Creates a coin in the current user's collection.

#### Request Body

```json
{
  "name": "1 рубль 1997",
  "country": "Россия",
  "year": 1997,
  "nominal": "1 рубль",
  "series": "Регулярный выпуск",
  "mint": "ММД",
  "metal": "мельхиор",
  "diameterMm": 20.5,
  "weightGrams": 3.25,
  "condition": "VF",
  "status": "owned",
  "quantity": 1,
  "purchaseDate": "2026-06-03",
  "purchasePrice": 100.0,
  "currency": "RUB",
  "catalogNumber": "Y# 604",
  "tags": ["Россия", "оборотные"],
  "notes": "Монета из основной коллекции."
}
```

#### Response `201 Created`

Returns the created coin details using the same shape as `GET /api/Coins/{coinId}`.

### `PUT /api/Coins/{coinId}`

Updates an existing coin owned by the current user.

#### Request Body

The request body uses the same editable fields as `POST /api/Coins`. Fields omitted from the request SHALL keep their previous values.

#### Response `200 OK`

Returns the updated coin details using the same shape as `GET /api/Coins/{coinId}`.

### `DELETE /api/Coins/{coinId}`

Deletes a coin owned by the current user.

#### Response `204 No Content`

The response body is empty.

### `GET /api/Coins/metadata`

Returns dictionaries required by the page filters and editors.

#### Response `200 OK`

```json
{
  "countries": ["Россия"],
  "metals": ["мельхиор"],
  "conditions": ["UNC", "XF", "VF", "F", "G"],
  "statuses": ["owned", "wanted", "sold", "duplicate"],
  "tags": ["Россия", "оборотные"],
  "currencies": ["RUB", "USD", "EUR"]
}
```

### `PUT /api/Coins/{coinId}/image`

Uploads or replaces the main coin image.

#### Request

The request SHALL use `multipart/form-data` with a required file field named `image`.

#### Response `200 OK`

```json
{
  "imageUrl": "/api/Coins/12/image"
}
```

### `DELETE /api/Coins/{coinId}/image`

Removes the main coin image.

#### Response `204 No Content`

The response body is empty.

## Validation Rules

- `name` is required and SHALL be at most 200 characters.
- `country` is required and SHALL be at most 100 characters.
- `year` is optional, but when present SHALL be between `-700` and the current year.
- `quantity` is required and SHALL be greater than or equal to `1`.
- `status` SHALL be one of `owned`, `wanted`, `sold`, or `duplicate`.
- `condition` SHALL be one of the configured condition dictionary values.
- `diameterMm`, `weightGrams`, and `purchasePrice` SHALL be greater than `0` when present.
- `currency` SHALL be an ISO 4217 code when `purchasePrice` is present.
- `tags` SHALL be unique after case-insensitive normalization.

## Error Responses

| Status | When |
| --- | --- |
| `400 Bad Request` | Query parameters or route values are malformed. |
| `401 Unauthorized` | The request has no valid authenticated session. |
| `404 Not Found` | The coin does not exist or does not belong to the current user. |
| `422 Unprocessable Entity` | The submitted coin data fails validation. |
