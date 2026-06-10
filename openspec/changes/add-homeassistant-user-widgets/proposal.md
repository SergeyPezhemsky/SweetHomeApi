# Add Home Assistant user widgets

## Why

The API already exposes a Home Assistant widget catalog, but users cannot persist selected smart-home widgets or group them by rooms.

## What Changes

- Add persisted user smart-home rooms.
- Add persisted user smart-home widgets linked to Home Assistant entity IDs.
- Allow widgets to be assigned to rooms.
- Expose authenticated endpoints to get and replace the user's smart-home layout.

## Impact

- New EF Core entities and migration.
- New `SmartHome` application service and repository.
- `SmartHomeController` gains user layout endpoints while keeping the existing catalog endpoint.
