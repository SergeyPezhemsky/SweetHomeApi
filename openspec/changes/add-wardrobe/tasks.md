## 1. Backend domain
- [ ] Wardrobe entities, enums, dictionaries.
- [ ] Rule-based classifier (RU/EN keywords, colour from hex).
- [ ] Outfit generator (weather, occasion, colour harmony, freshness, determinism).
- [ ] WardrobeService with validation-free business rules (ownership, cascade from item delete to outfits, wear tracking).

## 2. Backend persistence and API
- [ ] DbContext configuration, migration with designer attributes, idempotent schema guard.
- [ ] Repository.
- [ ] `api/v1/wardrobe` controller with validation and image upload.
- [ ] `wardrobe` default widget plus back-fill for existing users.

## 3. Tests
- [ ] xUnit project `SweetHomeApi.Tests` for classifier and generator scenarios from the spec.
- [ ] End-to-end smoke against PostgreSQL.

## 4. Client
- [ ] Lazy `wardrobe-module` with items, item form, outfit picker, outfits tab.
- [ ] Main-menu widget navigation and header integration.
- [ ] Production build.
