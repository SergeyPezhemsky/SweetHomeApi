# Design: flatlay and outfit builder

## Flatlay service (`flatlay/`)
- `flatlay.py` is the user's script, copied verbatim (still usable from the CLI).
- `app.py` (FastAPI) imports its functions: `load` → `segment` (rembg `isnet-general-use`) → `clean_mask` → `white_balance` → `compose(bg=#F9F7F5, ratio 3:4 to match the app's cards, size 1280, shadow)`. The rembg session is created once at startup; a lock keeps processing sequential (one model in memory, predictable RAM).
- Limits: input ≤ 15 MB, longest side downscaled to 2048 before segmentation; `RuntimeError("вещь не найдена")` → 422; anything else → 500.
- Image: `python:3.11-slim`, model downloaded during `docker build` into `/models` (`U2NET_HOME`), `OMP_NUM_THREADS=2`; compose `mem_limit: 2g`, no published ports, healthcheck on `/health`.

## Data model
`WardrobeItemImages` gains `ProcessedData bytea NULL`, `ProcessedContentType text NULL`, `ProcessingStatus text` (`NONE|PENDING|READY|FAILED`) and `Revision int` (incremented on every upload). `WardrobeItems` gains `ImageStatus text` and `ImageVariant text` (`ORIGINAL|PROCESSED`) so lists show the state without loading images. Migration `AddWardrobeFlatlay` + schema-guard `ALTER TABLE … ADD COLUMN IF NOT EXISTS`.

## Processing pipeline
- `IFlatlayClient` (typed `HttpClient`, timeout 180 s) and `IWardrobeImageQueue` (unbounded `Channel<string>`), consumed by one `BackgroundService`, so photos are processed one at a time.
- Upload: upsert original, clear processed bytes, `Revision++`, status `PENDING` (or `NONE` when `Flatlay:BaseUrl` is empty), variant `ORIGINAL`, enqueue item id.
- Worker: read `(Data, Revision)` of a `PENDING` image, call the service, then write the result with `UPDATE … WHERE ItemId = @id AND Revision = @rev` (a newer upload wins), set item status `READY`, variant `PROCESSED`, bump `ImageUpdatedAt` (client cache key). Errors → `FAILED` under the same revision check.
- Startup: enqueue all `PENDING` images.
- Image lock (`pg_advisory_xact_lock` on the item) is shared with upload/delete, so results never resurrect a deleted photo.

## API
- `GET items/{id}/image?variant=` — selects the column to read (only one blob is loaded).
- `PUT items/{id}/image/variant`, `POST items/{id}/image/reprocess`.
- `PUT outfits/{id}` — same validation as create, items re-checked under the outfits lock.

## Client
- `WardrobeItem` gets `imageStatus`/`imageVariant`; the image service cache key already includes `imageUpdatedAt`.
- List: pending badge; poll `GET items` every 3 s while any item is pending (max 2 min, only while the list is open).
- Item form: status line, toggle, retry; polls the single item while pending.
- Outfit builder component shared by create/edit, opened from the outfits tab and from picker suggestions (prefill passed via `WardrobeStateService`).
