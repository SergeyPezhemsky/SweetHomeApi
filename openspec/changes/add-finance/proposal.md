# Add finance tracking from receipt photos

## Why
Users want to see where their money goes without bookkeeping. The cheapest possible input is a photo of a receipt: the system extracts the total, splits it into line items and spending categories, and the finance tab shows a simple month view (total and categories). Detailed data (items, quantities, prices, recognition source) is kept under the hood for later analytics.

## What Changes
- New `Application.Modules.Finance` module: receipts, receipt items, a fixed category dictionary, per-user category rules learned from manual corrections, and a month summary that allocates each receipt total across its item categories.
- Receipt recognition through vision LLMs: an OpenAI-compatible endpoint (default: Yandex AI Studio, reachable from a Russian VPS) and Anthropic Claude (Messages API, optional proxy), in a configurable priority order with fallback. Recognition runs in a background worker with a revision guard, like wardrobe background removal. Without configuration recognition is disabled and photos are stored for manual entry.
- The same photo upload accepts a screenshot of bank purchase history and records each purchase separately on its transaction date.
- New authenticated REST API under `api/v1/finance`: dictionaries, receipt upload, manual receipt, list by month (optionally by category), receipt details and editing, re-recognition, receipt photo, delete, month summary.
- New tables `FinanceReceipts`, `FinanceReceiptImages`, `FinanceReceiptItems`, `FinanceCategoryRules` (migration plus idempotent schema guard).
- New main-menu widget `finance` for new users, back-filled for existing users.
- New Angular lazy module `/finance` in the SweetHome client.

## Impact
- Affected specs: `finance` (new), `widgets` (default widget back-fill).
- Affected code: `Application`, `Persistance`, `SweetHomeApi`, `SweetHomeApi.Tests`, `docker-compose.yml`, `.env.example`, client `src/app/modules/finance-module`, main menu, app routing.
- External dependency: LLM provider API keys (`FINANCE_LLM_*`, `FINANCE_ANTHROPIC_*` env vars). Receipt photos are sent to the configured providers.
