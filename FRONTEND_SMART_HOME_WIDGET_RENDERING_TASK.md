# Frontend task: smart-home widget rendering from backend controls

## Context

The smart-home screen currently may render a curtain/blinds widget as a simple on/off toggle. This is incorrect for Home Assistant `cover` entities that support percentage-based position control.

Backend already exposes the UI contract through `GET /api/SmartHome/widget-catalog`. The frontend should not infer controls only from `layout.widgets[].type` or from the Home Assistant domain. It must render controls from the catalog item matched by `entityId`.

## Goal

Render smart-home widgets using live backend metadata:

- `displayType`
- `controls`
- `capabilities`
- `state`
- `attributes`
- `unit`

Saved layout data should remain responsible only for user-owned configuration such as order, room, name override, icon, size, hidden state, and settings.

## Data sources

Load both endpoints when opening the Home screen:

```http
GET /api/SmartHome/layout
GET /api/SmartHome/widget-catalog
```

`GET /api/SmartHome/layout` returns the user's saved rooms and selected widgets.

`GET /api/SmartHome/widget-catalog` returns current Home Assistant state and UI-control metadata.

## Widget merge rule

For every saved widget from layout:

```ts
const catalogWidget = catalog.find(item => item.id === layoutWidget.entityId);
```

Use fields from `layoutWidget` for saved user configuration:

- `id`
- `entityId`
- `name`
- `icon`
- `roomId`
- `order`
- `size`
- `hide`
- `settingsJson`

Use fields from `catalogWidget` for live rendering and interaction:

- `state`
- `displayType`
- `controls`
- `capabilities`
- `attributes`
- `unit`
- `lastChanged`
- `lastUpdated`

If `catalogWidget` is missing, render a disabled/unavailable state. Do not fallback to an on/off toggle.

## Control rendering

Render concrete UI controls from `catalogWidget.controls[]`.

```ts
control.type === "toggle"      // switch/toggle
control.type === "button"      // button
control.type === "slider"      // range slider
control.type === "stepper"     // numeric stepper
control.type === "colorPicker" // color picker
```

For sliders and steppers, use:

- `control.min`
- `control.max`
- `control.step`
- `control.unit`
- `control.action`
- `control.label`

## Cover / curtain behavior

For Home Assistant `cover` widgets, backend returns:

```json
{
  "id": "cover.living_room_curtains",
  "type": "cover",
  "displayType": "cover",
  "state": "open",
  "controls": [
    { "type": "button", "action": "open", "label": "Open" },
    { "type": "button", "action": "close", "label": "Close" },
    { "type": "button", "action": "stop", "label": "Stop" },
    {
      "type": "slider",
      "action": "position",
      "label": "Position",
      "min": 0,
      "max": 100,
      "step": 1,
      "unit": "%"
    }
  ],
  "attributes": {
    "current_position": 45
  }
}
```

The frontend should render:

- Open button
- Close button
- Stop button
- Percentage slider when a slider control with `action: "position"` exists

The current slider value should come from:

```ts
catalogWidget.attributes?.current_position
```

If `current_position` is missing but the slider control exists, use a neutral fallback value such as `0` and show the control as available only if the backend marks it available through `controls`.

## Executing actions

All interactions should call:

```http
POST /api/SmartHome/actions
```

Button example:

```json
{
  "entityId": "cover.living_room_curtains",
  "action": "open"
}
```

Slider example:

```json
{
  "entityId": "cover.living_room_curtains",
  "action": "position",
  "value": 45
}
```

Use `control.action` as the action value. Do not translate frontend action names independently.

## Important implementation notes

- `layout.widgets[]` does not include live `controls`.
- `layout.widgets[].type` is not enough to decide which UI element to render.
- `displayType` selects the base widget layout.
- `controls[].type` selects the concrete input/button elements.
- A `cover` widget must not be rendered as a toggle unless backend explicitly returns a toggle control for it.
- If backend returns `displayType: "cover"` and a slider control, render the slider.

## Acceptance criteria

- A saved `cover` widget is rendered as a cover/curtain widget, not as an on/off toggle.
- If the matched catalog item contains `controls[].type === "slider"` and `action === "position"`, the UI shows a 0-100% slider.
- The slider value is initialized from `attributes.current_position` when present.
- Slider changes call `POST /api/SmartHome/actions` with `action: "position"` and numeric `value`.
- Button clicks call `POST /api/SmartHome/actions` with the exact `control.action`.
- If a saved widget has no matching catalog item, the UI shows an unavailable/disabled state instead of guessing a toggle.
- Rendering logic is covered by focused frontend tests for at least:
  - cover with position slider;
  - cover without position slider;
  - missing catalog item fallback.
