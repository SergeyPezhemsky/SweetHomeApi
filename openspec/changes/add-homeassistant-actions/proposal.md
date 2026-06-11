# Add Home Assistant actions

## Why
Home Assistant integration currently exposes live entity state for smart-home widgets, but the API cannot send commands back to Home Assistant. Users need to control writable devices from saved widgets, including setting smart curtain opening percentage.

## What Changes
- Add an authenticated endpoint for executing whitelisted Home Assistant widget actions.
- Map supported widget actions to Home Assistant service calls instead of exposing arbitrary service proxying.
- Expose a `position` slider control for cover entities that report `current_position`.
- Validate numeric action values before calling Home Assistant.

## Impact
- Affected spec: `smart-home`
- Affected code: `Application.Modules.HomeAssistant`, `SweetHomeApi.Controllers.SmartHome`, `SweetHomeApi.Infrastructure.HomeAssistant`
