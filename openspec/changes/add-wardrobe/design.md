# Design: wardrobe

## Data model

`WardrobeItem`
| field | type | notes |
|---|---|---|
| Id | text PK | Guid string |
| UserId | text FK AspNetUsers, cascade | owner |
| Name | text, 1..120 | required |
| Category | text (enum) | `TOP`, `BOTTOM`, `DRESS`, `OUTERWEAR`, `SHOES`, `ACCESSORY` |
| Colors | text[] | codes from the colour dictionary, 1..3 |
| Seasons | text[] | `SPRING`, `SUMMER`, `AUTUMN`, `WINTER`; empty = all-season |
| Styles | text[] | `CASUAL`, `OFFICE`, `FORMAL`, `SPORT`, `HOME`; at least one |
| Warmth | int 1..5 | 1 = very light, 5 = very warm |
| Brand | text? ≤ 80 | |
| Notes | text? ≤ 1000 | |
| IsFavorite | bool | |
| HasImage | bool | photo bytes live in `WardrobeItemImages` |
| ImageUpdatedAt | timestamptz? | used as cache-buster |
| WearCount | int | |
| LastWornAt | timestamptz? | |
| CreatedAt / UpdatedAt | timestamptz | |

`WardrobeOutfit`: Id, UserId, Name (1..80), ItemIds text[] (2..8, all owned by user), Occasion (style code), Temperature int?, Note ≤ 500, WearCount, LastWornAt, CreatedAt, UpdatedAt.

`WardrobeItemImage`: ItemId (PK, FK → WardrobeItems, cascade), Data bytea (≤ 5 MB, JPEG/PNG/WEBP verified by magic bytes), ContentType, UpdatedAt.

Images are stored in the database (home-scale project, single Postgres volume) in a separate table so item lists never load photo bytes; they are served by a dedicated endpoint. nginx `client_max_body_size` for `/api/` is raised to 6 MB.

## Classifier
Pure function `WardrobeClassifier.Classify(name, notes, colorHex)`:
- Lower-cases text, normalises `ё`→`е`, tokenises, matches against a keyword table (Russian stems, English whole words incl. plurals and phrases). The head noun decides the category: first Russian noun (adjectives ignored), otherwise last English noun. Each rule contributes category, warmth, seasons and styles.
- Colour: explicit colour words in the text win; otherwise the photo's dominant colour hex is mapped to the nearest dictionary colour (Euclidean distance in RGB with a neutral/greyscale check).
- Returns suggestion + `confidence` (0..1) + `matchedKeywords`. Unknown text returns `category = null`, confidence 0 (client keeps user's choice).

## Outfit generator
Pure `OutfitGenerator.Generate(items, request, now)`:
1. Target warmth from temperature: ≥ 25 → 1, 18..24 → 2, 10..17 → 3, 0..9 → 4, < 0 → 5. Season from temperature if given, else from month.
2. Filter by occasion (item styles contain occasion) and season (item seasons empty or contain season).
3. Templates: `TOP + BOTTOM + SHOES` or `DRESS + SHOES`; `OUTERWEAR` (warmth within ±1 of target) required when temperature < 15, forbidden when > 22, optional otherwise; base layers within ±2 of the target warmth (±1 in hot weather); at most one `ACCESSORY`, added only when it does not lower the score (+2 completeness bonus).
4. Score (0..100): warmth fit, colour harmony (neutrals go with everything; ≤ 2 accent colour families; same-family monochrome bonus), freshness (not worn in the last 3 days, low wear count), favourites bonus, small deterministic jitter from `seed` for variety.
5. Picks `count` (1..10, default 3) outfits greedily with a reuse penalty (distinct item sets, minimal item reuse) and returns them sorted by score, each with Russian `reasons`. When nothing can be built, returns an empty list plus `missing` hints (e.g. "Добавьте обувь для стиля «Офис»").
