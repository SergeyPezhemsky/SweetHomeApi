## 1. Flatlay service
- [ ] `flatlay/` with the user's script, FastAPI wrapper, Dockerfile with baked model.
- [ ] docker-compose service, API wiring via `Flatlay:BaseUrl`.

## 2. API
- [ ] Migration and schema guard for image status/variant/revision.
- [ ] Background queue + worker + typed client; revision-safe writes.
- [ ] Variant/reprocess endpoints; `PUT outfits/{id}`.
- [ ] Tests.

## 3. Client
- [ ] Status badges, polling, original/clean toggle, retry.
- [ ] Outfit builder (create/edit), entry points from outfits tab and picker.
