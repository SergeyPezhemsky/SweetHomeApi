# Wardrobe Specification — delta

## ADDED Requirements

### Requirement: Flatlay processing service
A separate `flatlay` container SHALL wrap the user's `flatlay.py` pipeline (rembg segmentation, mask cleanup, white balance, composition on a light background with a soft shadow) and expose `POST /process` (multipart `file`) returning a JPEG in the 3:4 proportion of the app cards, plus `GET /health`. The rembg model SHALL be baked into the image at build time (no download at runtime). The service SHALL NOT be published on a host port.

#### Scenario: Clean photo
- WHEN a photo of a garment on a cluttered background is posted to `/process`
- THEN the response is a JPEG (3:4, light background) with the garment cut out and centred.

#### Scenario: Nothing found
- WHEN the image contains no detectable object
- THEN `/process` returns `422` and the API keeps the original photo.

### Requirement: Automatic background removal
After every successful photo upload the API SHALL keep the original bytes and queue background processing; the upload response SHALL return immediately with `imageStatus = PENDING`.

#### Scenario: Processing succeeds
- GIVEN a photo was uploaded
- WHEN the flatlay service returns a result
- THEN the item has `imageStatus = READY`, `imageVariant = PROCESSED`, a new `imageUpdatedAt`, and `GET items/{id}/image` returns the processed JPEG.

#### Scenario: Processing fails or service is down
- WHEN the flatlay service errors, times out or is unreachable
- THEN the item has `imageStatus = FAILED`, the original photo is shown, and nothing else breaks.

#### Scenario: Restart
- WHEN the API restarts while items are `PENDING`
- THEN they are processed after startup.

#### Scenario: Superseded upload
- WHEN a new photo is uploaded while an older one is still being processed
- THEN the older result is discarded and only the newest photo's result is stored.

#### Scenario: Disabled
- WHEN `Flatlay:BaseUrl` is not configured
- THEN uploads behave as before with `imageStatus = NONE`.

### Requirement: Photo variants
- `GET items/{id}/image?variant=original|processed` SHALL return the requested variant (404 `WARDROBE_IMAGE_NOT_FOUND` if it does not exist); without `variant` the item's selected variant is returned, falling back to the original while processing is not ready.
- `PUT items/{id}/image/variant` with `{ variant: ORIGINAL | PROCESSED }` SHALL select the displayed variant (400 if `PROCESSED` is not ready).
- `POST items/{id}/image/reprocess` SHALL queue processing again (404 without a photo).
- Item DTOs SHALL expose `imageStatus` (`NONE`, `PENDING`, `READY`, `FAILED`) and `imageVariant` (`ORIGINAL`, `PROCESSED`).
- Classification by photo colour keeps using the client-computed colour of the original photo.

### Requirement: Outfit editing
`PUT api/v1/wardrobe/outfits/{id}` SHALL replace name, item ids, occasion, temperature and note with the same validation as creation (2..8 distinct owned items, name 1..80, note ≤ 500, known occasion, temperature −60..60). Foreign or missing outfit → 404 `WARDROBE_OUTFIT_NOT_FOUND`; foreign or missing items → 400 `itemIds`. Wear statistics are preserved.

### Requirement: Client — photos
- Cards and outfit thumbnails show the displayed variant; items with `PENDING` show a small «убираем фон» indicator and the list/form poll until processing finishes (bounded, stops when leaving).
- The item form shows the processing state and, when the processed photo is ready, a toggle «Без фона / Оригинал»; on `FAILED` it shows «Не удалось убрать фон» with «Повторить».

### Requirement: Client — outfit builder
- `/wardrobe/outfits/create` and `/wardrobe/outfits/:outfitId/edit`: name, occasion chips, optional temperature, note, the chosen items grouped by category, and an item picker with category chips and search to add or remove items (2..8). Warnings (not errors) when the outfit has no shoes or no top/bottom/dress. Save, delete (edit), unsaved-changes guard.
- Outfits tab: «Собрать образ» button and «Изменить» on every saved outfit.
- Outfit picker: «Изменить» on each suggestion opens the builder prefilled with its items, occasion and temperature; saving there creates a new outfit.
- Back navigation never returns to a saved or deleted builder form.
