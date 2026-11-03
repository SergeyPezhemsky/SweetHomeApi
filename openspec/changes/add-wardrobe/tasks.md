## 1. Backend domain
- [x] Wardrobe entities, enums, dictionaries.
- [x] Rule-based classifier (RU/EN keywords, colour from hex).
- [x] Outfit generator (weather, occasion, colour harmony, freshness, determinism).
- [x] WardrobeService with validation-free business rules (ownership, cascade from item delete to outfits, wear tracking).

## 2. Backend persistence and API
- [x] DbContext configuration, migration with designer attributes, idempotent schema guard.
- [x] Repository.
- [x] `api/v1/wardrobe` controller with validation and image upload.
- [x] `wardrobe` default widget plus back-fill for existing users.

## 3. Tests
- [x] xUnit project `SweetHomeApi.Tests` for classifier and generator scenarios from the spec.
- [x] End-to-end smoke against PostgreSQL.

## 4. Client
- [x] Lazy `wardrobe-module` with items, item form, outfit picker, outfits tab.
- [x] Main-menu widget navigation and header integration.
- [x] Production build.
