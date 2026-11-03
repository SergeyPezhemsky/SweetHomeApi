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
Pure function `WardrobeClassifier.Classify(name, notes, colorHex)`. Only the name and the photo colour are used: notes usually describe other garments («к белой футболке») and would distort the result.
- Lower-cases text, normalises `ё`→`е`, tokenises, matches against a keyword table (Russian stems, English whole words incl. plurals and phrases). The head noun decides the category: first Russian noun (adjectives ignored), otherwise last English noun. Each rule contributes category, warmth, seasons and styles.
- Colour: explicit colour words in the text win; otherwise the photo's dominant colour hex is mapped to the nearest reference colour (weighted RGB distance over the dictionary palette plus extra anchor shades, greyscale handled by lightness; greys are skipped for clearly saturated pixels).
- Returns suggestion + `confidence` (0..1) + `matchedKeywords`. Unknown text returns `category = null`, confidence 0 (client keeps user's choice).

## Outfit generator
Pure `OutfitGenerator.Generate(items, request, now)`:
1. Desired warmth is continuous: `1 + (25 − t) / 10`, clamped to 1..5 (+25 °C → 1, +15 → 2, +5 → 3, −5 → 4, −15 → 5); the integer `targetWarmth` (≥ 25 → 1, 18..24 → 2, 10..17 → 3, 0..9 → 4, < 0 → 5) is used for slot filters. Occasion `HOME` is indoor: +22 °C, no outerwear, no season filter.
2. Allowed seasons come from the temperature, not the calendar: ≥ 15 °C summer, 1..21 °C spring/autumn, ≤ 5 °C winter (overlaps intended); without a temperature the typical Moscow temperature for the month is assumed. Items with no seasons are all-season. Items must have the occasion style; for street slots (outerwear, shoes, warm accessories — the latter per body place) the generator falls back to `CASUAL` items when the occasion has none (winter boots for the office); for `SPORT` the fallback prefers items not tagged `FORMAL`/`OFFICE` (a puffer, not a fur coat).
3. Templates: `TOP + BOTTOM + SHOES` or `DRESS + SHOES`. Outerwear is required below +15 °C (warmth ≥ target − 1, at least 4 up to +5 °C, only warmth 5 below −10 °C), forbidden above +22 °C, optional in between with a penalty that grows with temperature. Base-layer tolerance around the desired warmth (capped at 4): ±1.2 in warm weather, ±1.5 for tops/dresses in deep frost, ±2 otherwise (bottoms keep ±2 in frost). Accessories: one per body "place" (head, neck, hands, bag, waist, other), up to three in the cold and one otherwise, each added only if it does not lower the score (+2 completeness bonus); no tie/bow tie with a dress.
4. Score (0..100): warmth fit, street coherence (outerwear, shoes and warm accessories within one warmth step of each other), colour harmony (neutrals go with everything; shades of one colour count as one accent family; ≤ 2 families preferred; known clashing pairs penalised), freshness (not worn in the last 3 full days, slight preference for rarely worn items), favourites bonus, small deterministic jitter from `seed`.
5. Picks `count` (1..10, default 3) outfits greedily with a reuse penalty (distinct item sets, minimal item reuse, never more than 20 points below the best) and returns them sorted by score, each with Russian `reasons`. When nothing can be built, returns an empty list plus `missing` hints.
