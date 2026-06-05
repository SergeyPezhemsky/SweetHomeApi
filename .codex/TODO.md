# SweetHomeApi Working Notes

## Recommended First Tasks

1. Restrict Swagger to development or protect it in production.
2. Decide whether startup migrations are acceptable for the home deployment; otherwise move migrations to an explicit deployment step.
3. Improve health data types for values that are numeric or structured.
4. Add minimal .NET tests around health upsert behavior and widgets ownership.

## Useful Follow-up Checks

- Inspect the frontend that consumes `api/Widgets` and `api/Health`, if it exists outside this repository.
- Verify production cookie/CORS behavior behind nginx.
- Add minimal .NET tests around health upsert behavior and widgets ownership.
