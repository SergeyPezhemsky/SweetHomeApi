# Design

## Data Model

`SmartHomeRoom` stores user-owned rooms with a stable string id, display name, icon, sort order, and hidden flag.

`SmartHomeWidget` stores user-owned widgets selected from Home Assistant. The widget keeps the Home Assistant `EntityId`, display metadata, type, size/order/hidden state, optional room binding, and JSON settings for future per-widget options.

Both entities reference `AspNetUsers` with cascade delete. Widget room binding is optional and restricted to the same user's room at service level.

## API

`GET /api/SmartHome/layout` returns:

- `rooms`
- `widgets`

`PUT /api/SmartHome/layout` replaces the current user's rooms and widgets with the submitted layout. Unknown room ids referenced by widgets are normalized to `null`.

The existing `GET /api/SmartHome/widget-catalog` continues to fetch live entities from Home Assistant.
