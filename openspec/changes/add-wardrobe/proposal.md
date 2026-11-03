# Add wardrobe classification and outfit selection

## Why
SweetHome users want to keep a catalogue of their clothes, have new items classified automatically (category, season, style, warmth, colour) and get outfit suggestions for the occasion and the weather instead of picking clothes by hand every morning.

## What Changes
- New `Application.Modules.Wardrobe` module: wardrobe items, saved outfits, a rule-based item classifier (Russian and English keywords plus colour from the photo) and a deterministic outfit generator scored by weather fit, colour harmony, freshness and favourites.
- New authenticated REST API under `api/v1/wardrobe` (items CRUD, item photos, classification, dictionaries, outfit suggestions, saved outfits, "wear" tracking).
- New tables `WardrobeItems` and `WardrobeOutfits` (migration plus idempotent schema guard, like Movies).
- New main-menu widget `wardrobe` for new users and back-filled for existing users.
- New Angular lazy module `/wardrobe` in the SweetHome client.
- New xUnit test project `SweetHomeApi.Tests` covering classifier and outfit generator.

## Impact
- Affected specs: `wardrobe` (new), `widgets` (default widget back-fill).
- Affected code: `Application`, `Persistance`, `SweetHomeApi`, `SweetHomeApi.Tests`, client `src/app/modules/wardrobe-module`, main menu, header, app routing.
