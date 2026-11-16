# Finance Specification

## ADDED Requirements

### Requirement: Ownership and authentication
Every endpoint under `api/v1/finance` SHALL require an authenticated session and SHALL only read or modify receipts owned by the current user.

#### Scenario: Foreign receipt
- GIVEN receipt R belongs to user A
- WHEN user B reads, edits, deletes, re-recognizes or fetches the photo of R
- THEN the system returns `404` (`FINANCE_RECEIPT_NOT_FOUND`) and R is unchanged.

### Requirement: Dictionaries
`GET api/v1/finance/dictionaries` SHALL return categories (`code`, `name`, `icon`, `color`) and `recognition` (whether photo recognition is configured).

### Requirement: Receipt upload
`POST api/v1/finance/receipts` (multipart: `file`, optional `purchasedOn` as `yyyy-MM-dd`) SHALL accept JPEG, PNG or WEBP up to 5 MB verified by magic bytes, create a receipt with `source = PHOTO`, `total = 0`, `category = OTHER`, `purchasedOn` = the given date or today (UTC), and return `201`.

#### Scenario: Recognition enabled
- WHEN recognition is configured
- THEN the receipt is created with `status = PENDING` and queued.

#### Scenario: Recognition disabled
- WHEN recognition is not configured
- THEN the receipt is created with `status = FAILED`, `failureReason = DISABLED`, and the photo is kept.

### Requirement: Recognition result
The worker SHALL fill store, date, currency, total, receipt category and items from the provider answer and set `status = READY`, unless the receipt revision changed meanwhile.

#### Scenario: Photo is not a receipt
- WHEN the provider answers `isReceipt = false`
- THEN `status = FAILED`, `failureReason = NOT_A_RECEIPT`.

#### Scenario: Learned rule
- GIVEN the user previously moved the item «Корм Purina Pro Plan 1,5 кг» to `PETS`
- WHEN a new receipt contains «Корм Purina для щенков 3 кг» recognized as `GROCERIES`
- THEN the item gets category `PETS` with `categorySource = RULE` (same key «корм purina»).

#### Scenario: Bank screenshot with purchases on different dates
- GIVEN an uploaded bank history screenshot with multiple completed purchases
- WHEN recognition returns a complete list
- THEN each purchase becomes a separate `READY` expense with its own date, total, currency and category.
- AND the original upload retains the screenshot; repeat recognition of imported purchases returns `409`.
- AND incoming payments, transfers, refunds and balance figures are excluded.

#### Scenario: Time after a date without a year
- GIVEN an image uploaded on 2026-11-16
- WHEN a purchase date reads `14.11 18:32`
- THEN the purchase is recorded on 2026-11-14.

### Requirement: Provider fallback
The worker SHALL try configured providers in `Finance:Recognition:Order`; when a provider fails or cannot read the receipt, the next provider SHALL be tried, and the receipt SHALL record the model that produced the stored answer in `recognitionModel`.

#### Scenario: Yandex cannot read, Claude can
- GIVEN order `openai,anthropic` and both providers configured
- WHEN the OpenAI-compatible provider answers without a JSON receipt and Anthropic answers with a valid receipt
- THEN the receipt is `READY` with Anthropic's data and `recognitionModel` = the Anthropic model.

### Requirement: Manual receipt
`POST api/v1/finance/receipts/manual` with `{ total, category, purchasedOn?, store?, note? }` SHALL create a `READY` receipt without items and photo. `total` 0.01..10 000 000, category known, store ≤ 120, note ≤ 500, `purchasedOn` from 2000-01-01 to tomorrow (UTC) — the same date rule applies to upload and editing.

### Requirement: Receipt list
`GET api/v1/finance/receipts?month=yyyy-MM[&category=CODE]` SHALL return the month's receipts (all statuses) sorted by `purchasedOn` desc then `createdAt` desc, without items. With `category`, only `READY` RUB receipts that allocate a non-zero amount to the category are returned, each with `categoryAmount` (they add up to the summary amount of the category).

### Requirement: Receipt details and editing
- `GET api/v1/finance/receipts/{id}` SHALL return the receipt with items and `itemsTotal`.
- `PUT api/v1/finance/receipts/{id}` with `{ purchasedOn, store?, total, currency?, category, note?, items[] }` SHALL replace the receipt fields and items, set `status = READY`, clear `failureReason`, bump the revision. Items: ≤ 150, name 1..200, amount −10 000 000..10 000 000 and non-zero after rounding to kopecks, quantity/price optional ≥ 0, known category.
- An item whose category differs from the stored category of the same item (same id) SHALL get `categorySource = USER` and upsert the user's rule for its key.
- `DELETE api/v1/finance/receipts/{id}` SHALL delete the receipt with items and photo (`204`).

### Requirement: Re-recognition
`POST api/v1/finance/receipts/{id}/recognize` SHALL set `status = PENDING` with a new revision and queue the receipt; `404` when the receipt has no photo (`FINANCE_IMAGE_NOT_FOUND`), `503` (`FINANCE_RECOGNITION_DISABLED`) when recognition is not configured.

### Requirement: Receipt photo
`GET api/v1/finance/receipts/{id}/image` SHALL return the stored photo bytes or `404`.

### Requirement: Month summary
`GET api/v1/finance/summary?month=yyyy-MM` SHALL return `{ month, total, receiptCount, pendingCount, failedCount, otherCurrencyCount, previousTotal, categories[] }`, where categories are sorted by amount desc, contain only non-zero amounts, and add up exactly to `total`.

#### Scenario: Discount allocation
- GIVEN a READY receipt with total 900, items GROCERIES 600 and HOME 400
- THEN the summary shows GROCERIES 540 and HOME 360.

## MODIFIED Requirements

### Requirement: Default widgets
The default widget set SHALL include `finance` («Финансы», icon `account_balance_wallet`), and it SHALL be back-filled for existing users.
