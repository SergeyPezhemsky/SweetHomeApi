# Wardrobe Specification

## ADDED Requirements

### Requirement: Ownership and authentication
Every endpoint under `api/v1/wardrobe` SHALL require an authenticated session and SHALL only read or modify data owned by the current user.

#### Scenario: Anonymous request
- WHEN an anonymous client calls any `api/v1/wardrobe` endpoint
- THEN the system returns `401`.

#### Scenario: Foreign item
- GIVEN item X belongs to user A
- WHEN user B requests, updates, deletes, wears, or fetches the image of X, or uses X in an outfit
- THEN the system returns `404` (`WARDROBE_ITEM_NOT_FOUND`) or `400` validation error for outfits, and X is unchanged.

### Requirement: Dictionaries
`GET api/v1/wardrobe/dictionaries` SHALL return categories, seasons, styles, colours (`code`, Russian `name`, `hex`, `neutral`) and warmth levels (1..5 with Russian names).

### Requirement: Item CRUD
- `GET api/v1/wardrobe/items` SHALL return the user's items without image bytes, each with `hasImage` and `imageUpdatedAt`, filterable by `query` (name/brand/notes, case-insensitive), `category`, `season`, `style`, `color`, `favorite`, sorted by `createdAt` desc, plus `total` (matching items) and `counts` (items per category in the whole wardrobe, all six categories present).
- `POST api/v1/wardrobe/items` SHALL validate: name required ≤ 120; category valid; 1..3 known colours; known seasons; ≥ 1 known style; warmth 1..5; brand ≤ 80; notes ≤ 1000. Errors use the existing `ErrorResponseDto` shape with `VALIDATION_ERROR` and per-field details. Success returns `201` with the created item.
- `GET/PUT/DELETE api/v1/wardrobe/items/{id}` SHALL behave like Movies (404 `WARDROBE_ITEM_NOT_FOUND`).
- Deleting an item SHALL remove it from saved outfits; outfits left with fewer than 2 items SHALL be deleted.

### Requirement: Item photo
- `PUT api/v1/wardrobe/items/{id}/image` (multipart field `file`) SHALL accept JPEG, PNG or WEBP up to 5 MB, verified by magic bytes, and reject anything else with `400`.
- `GET api/v1/wardrobe/items/{id}/image` SHALL return the bytes with the stored content type, or `404` when there is no photo.
- `DELETE api/v1/wardrobe/items/{id}/image` SHALL remove the photo.

### Requirement: Classification
`POST api/v1/wardrobe/classify` with `{ name, notes?, colorHex? }` SHALL return `{ category, seasons, styles, warmth, colors, confidence, matchedKeywords }`.

#### Scenario: Russian keywords
- WHEN name is "Тёплый шерстяной свитер серый"
- THEN category is `TOP`, warmth ≥ 4, seasons include `WINTER`, colours include `GRAY`.

#### Scenario: Shoes by English keyword
- WHEN name is "White sneakers"
- THEN category is `SHOES`, styles include `CASUAL` or `SPORT`, colours include `WHITE`.

#### Scenario: Colour from photo
- WHEN name has no colour word and `colorHex` is `#1F2A44`
- THEN colours contain `NAVY`.

#### Scenario: Unknown text
- WHEN name is "Штука"
- THEN category is null and confidence is 0.

### Requirement: Outfit suggestions
`POST api/v1/wardrobe/outfits/suggest` with `{ occasion, temperature?, count?, seed? }` SHALL return `{ outfits: [{ items, score, reasons }], targetWarmth, season, missing }`.

#### Scenario: Cold weather
- GIVEN the user has tops, bottoms, shoes and outerwear for `CASUAL`
- WHEN temperature is -5
- THEN every outfit contains exactly one `OUTERWEAR` item.

#### Scenario: Hot weather
- WHEN temperature is 28
- THEN no outfit contains `OUTERWEAR` and the warmest pieces are avoided.

#### Scenario: Occasion filter
- WHEN occasion is `OFFICE`
- THEN every item in every outfit has style `OFFICE`.

#### Scenario: Valid structure
- THEN each outfit is either `TOP + BOTTOM + SHOES` or `DRESS + SHOES`, plus optional outerwear and at most one accessory, and outfits have distinct item sets.

#### Scenario: Not enough clothes
- GIVEN the user has no shoes for the occasion
- THEN `outfits` is empty and `missing` explains what to add.

#### Scenario: Determinism
- WHEN the same request with the same `seed` is repeated without data changes
- THEN the same outfits are returned.

### Requirement: Saved outfits and wear tracking
- `GET/POST api/v1/wardrobe/outfits`, `DELETE api/v1/wardrobe/outfits/{id}` SHALL manage saved outfits (2..8 owned items, name ≤ 80).
- `POST api/v1/wardrobe/outfits/{id}/wear` and `POST api/v1/wardrobe/items/{id}/wear` SHALL increment `wearCount` and set `lastWornAt` on the outfit and each of its items.

### Requirement: Main menu widget
New users SHALL get a `wardrobe` widget ("Гардероб", icon `checkroom`); existing users SHALL get it appended (visible, last order) the next time they load `GET /api/Widgets`, exactly once.

### Requirement: Client
The Angular client SHALL provide `/wardrobe` with:
- Items tab: search, category chips with counts, photo grid cards (photo or category icon placeholder, name, colour dots, favourite toggle), empty state with CTA.
- Item form (`/wardrobe/items/create`, `/wardrobe/items/:id/edit`): photo pick/replace/remove with client-side resize (longest side ≤ 1280 px, JPEG), dominant colour extraction, automatic classification as the user types (debounced) that fills untouched fields and marks them «авто», manual overrides kept, validation messages, delete with confirmation, unsaved-changes guard.
- Outfit picker (`/wardrobe/outfits/suggest`): occasion chips, temperature input (default by season), "Подобрать", outfit cards with item thumbnails, score and reasons, "Ещё варианты" (new seed), "Сохранить", "Надеть сегодня"; `missing` hints when empty.
- Outfits tab: saved outfits with thumbnails, wear count, "Надеть", delete.
- Header back navigation, page titles and an add button on the items list; main-menu widget opens `/wardrobe`.
- Works at 375 px phone width without horizontal scroll and matches the existing dark glass visual style.
- `ng build` (production) passes with no new errors.
