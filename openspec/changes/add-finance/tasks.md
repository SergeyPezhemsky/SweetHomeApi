## 1. Backend domain
- [x] Finance entities, enums, category dictionary.
- [x] Recognition answer parser and normalizer (tolerant JSON, dates, totals, rules).
- [x] Allocation of receipt totals to categories; month summary.
- [x] Item key normalization and user rules.
- [x] FinanceService (ownership, statuses, revisions, editing, learning).

## 2. Backend persistence and API
- [x] DbContext configuration, migration, idempotent schema guard, revision sequence.
- [x] Repository with atomic status updates.
- [x] OpenAI-compatible recognizer, queue and background worker.
- [x] `api/v1/finance` controller with validation and photo upload.
- [x] `finance` default widget plus back-fill.
- [x] docker-compose / .env.example settings.

## 3. Tests
- [x] Parser, allocation, normalization, service and worker tests.

## 4. Client
- [x] Lazy `finance-module`: month view, upload, receipt details/editing, manual entry.
- [x] Main-menu widget navigation.
- [x] Production build.

## 5. Review
- [x] Independent review rounds (gauntlet loop, 5 rounds; round 4 backend clean, round 5 one low client defect fixed and verified).
