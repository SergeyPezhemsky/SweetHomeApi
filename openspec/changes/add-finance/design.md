# Design: finance

## Goals and non-goals
- Goal: one action to record spending (photo), one screen to understand the month (total + categories).
- Goal: keep detailed data (items, quantities, prices, category source, raw model output) for later analysis.
- Non-goal (v1): shared family budget, incomes, budgets/limits, custom categories, multi-photo receipts, bank imports, FNS QR lookup.

## Data model

`FinanceReceipt`
| field | type | notes |
|---|---|---|
| Id | text PK | Guid string |
| UserId | text FK AspNetUsers, cascade | owner |
| Source | text enum | `PHOTO`, `MANUAL` |
| Status | text enum | `PENDING`, `READY`, `FAILED` |
| FailureReason | text enum? | `NOT_A_RECEIPT`, `UNREADABLE`, `PROVIDER_ERROR`, `DISABLED`, `INTERNAL_ERROR` (answer received but could not be saved) |
| PurchasedOn | date | from the receipt; fallback is the client's local date at upload; never later than tomorrow (UTC) |
| UploadedOn | date | client's local date at upload (manual: purchase date); never changes, it is the reference for date plausibility on every (re-)recognition |
| Store | varchar(120)? | |
| Total | numeric(12,2) | authoritative amount of the receipt, ≥ 0 |
| Currency | varchar(3) | ISO code, default `RUB` |
| Category | text enum | receipt-level category, used when there are no items |
| Note | varchar(500)? | |
| HasImage | bool | photo bytes in `FinanceReceiptImages` |
| Revision | int | from sequence `FinanceReceiptRevisionSeq`; recognition result is saved only for the revision it was started for |
| RecognitionJson | text? | raw model answer (debug / re-parse) |
| CreatedAt / UpdatedAt | timestamptz | |

`FinanceReceiptItem`: Id, ReceiptId (FK cascade), Position, Name (varchar 200), Quantity numeric(12,3)?, Price numeric(12,2)?, Amount numeric(12,2) (may be negative for discounts), Category, CategorySource (`AI`, `RULE`, `USER`).

`FinanceReceiptImage`: ReceiptId (PK, FK cascade), Data bytea (≤ 5 MB, JPEG/PNG/WEBP by magic bytes), ContentType.

`FinanceCategoryRule`: (UserId, Key) PK, Category, UpdatedAt. Key is a normalized item name (lower case, `ё`→`е`, letters only, first two words of ≥ 3 letters).

## Categories
Fixed dictionary with codes, Russian names, Material icon and chart colour: GROCERIES, CAFE, TRANSPORT, CAR, HOME, HEALTH, BEAUTY, CLOTHES, PETS, ELECTRONICS, ENTERTAINMENT, SERVICES, UTILITIES, TRAVEL, GIFTS, EDUCATION, OTHER.

## Recognition
- `IReceiptRecognizer` in Application; implementation `OpenAiCompatibleReceiptRecognizer` posts `chat/completions` with the image as a base64 data URL and a prompt that lists category codes and requires a JSON object (`isReceipt`, `store`, `date`, `currency`, `total`, `category`, `items[]`).
- Config section `Finance:Recognition`: `BaseUrl` (default `https://ai.api.cloud.yandex.net/v1`), `ApiKey`, `AuthScheme` (`Api-Key` for Yandex, `Bearer` otherwise), `Project` (Yandex folder id, sent as `OpenAI-Project`), `Model` (`gpt://<folder>/<model>` is built when the value has no scheme and `Project` is set), `TimeoutSeconds`, `MaxTokens` (12000), `MaxResponseBytes` (2 MB). Empty key or model disables recognition; a Yandex URL without folder id (and without a full `gpt://` model URI) also disables it with a startup warning.
- Answer selection: text up to the last `</think>` and any `<think>…</think>` blocks are dropped. Candidates are complete JSON objects plus one truncated object repaired up to the last complete value (its span is only the used prefix). Candidates that echo the prompt example (item placeholder, or store placeholder together with the example total) are discarded. The winner has most receipt keys (`isReceipt`, `total`, `items`, `store`, `date`, `currency`; `category` counts only together with them, so item objects score 0); ties go to a complete object over a repaired one, then to the later one. A copied store placeholder becomes null, a copied currency description becomes RUB. A repaired answer without an explicit total is UNREADABLE (surviving items are only part of the receipt). Parsing scans at most 256 KB and 200 object starts.
- Currency: empty or rouble notations → `RUB`; known symbols → ISO codes; anything else → `XXX`, which keeps it out of rouble totals.
- Date: recognized date is accepted when it is within [upload date − 3 years, min(upload date + 1, tomorrow UTC)]; if the model gives no or an implausible date, the current receipt date is kept.
- Parsing is tolerant: `<think>` reasoning blocks, code fences and text around the JSON are ignored (see answer selection above). Amount strings: spaces, NBSP, «р.»/«руб.», `−` (U+2212) and other dashes; a separator is decimal only when followed by 1–2 digits (3 for quantity); letters between digits make the value invalid. Unknown categories become `OTHER`; items without a name or non-zero amount are dropped; at most 150 items. Text is cleaned of control characters and lone surrogates and truncated on a surrogate boundary.
- After parsing, user rules override AI categories (`CategorySource = RULE`).
- Total: the model total when present; otherwise the sum of items, unless the answer was repaired (then UNREADABLE). Date: see the date rule above.
- Worker: one receipt at a time; provider unavailability (network, 429, 5xx) is retried with delay; on final failure status becomes `FAILED` with `PROVIDER_ERROR`; non-receipt photo → `FAILED/NOT_A_RECEIPT`; unparsable → `FAILED/UNREADABLE`; a failure to save the answer → `FAILED/INTERNAL_ERROR` (raw answer kept). If even that write fails (database down), the receipt is re-queued after `InternalRetryDelaySeconds`. On startup PENDING receipts are re-queued; when recognition is disabled they become `FAILED/DISABLED`.
- Editing a receipt or re-recognition bumps the revision, so an in-flight result is discarded.

## Month summary and allocation
Only `READY` receipts in `RUB` are summed; other currencies are reported as `otherCurrencyCount`. Each receipt contributes exactly its `Total`:
- no items → whole total goes to the receipt category;
- items → totals per item category are scaled proportionally so they add up to the receipt total (largest-remainder rounding to kopecks). If the item sum is ≤ 0 (or no category has a positive sum) the total goes to the receipt category.
The same allocation and the same RUB-only rule are used for the category filter of the receipt list, so a receipt row shows the amount that category got in the summary.

## Learning from corrections
When a user changes the category of an item in `PUT receipts/{id}`, the rule for that item key is upserted. Rules apply to future recognitions only.

## Concurrency
Status/result writes use atomic `UPDATE ... WHERE Revision = @rev AND Status = 'PENDING'`. Editing replaces items inside a transaction under an advisory lock per receipt.

## Privacy
Receipt photos are sent to the configured provider. Recognition can be disabled by leaving the key empty.
