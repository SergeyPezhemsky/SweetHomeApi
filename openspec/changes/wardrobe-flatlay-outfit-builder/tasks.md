## 1. Flatlay service
- [x] `flatlay/` with the user's script, FastAPI wrapper, Dockerfile with baked model.
- [x] docker-compose service, API wiring via `Flatlay:BaseUrl`.

## 2. API
- [x] Migration and schema guard for image status/variant/revision.
- [x] Background queue + worker + typed client; revision-safe writes.
- [x] Variant/reprocess endpoints; `PUT outfits/{id}`.
- [x] Tests.

## 3. Client
- [x] Status badges, polling, original/clean toggle, retry.
- [x] Outfit builder (create/edit), entry points from outfits tab and picker.
