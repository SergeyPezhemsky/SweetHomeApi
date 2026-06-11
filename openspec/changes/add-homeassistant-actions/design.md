# Design

## Endpoint
`POST /api/SmartHome/actions` accepts:

```json
{
  "entityId": "cover.living_room_curtains",
  "action": "position",
  "value": 45
}
```

The endpoint returns `204 No Content` after Home Assistant accepts the service call.

## Service Mapping
The application service derives the Home Assistant domain from `entityId` and maps only known actions to Home Assistant services:

| Domain | Action | Home Assistant service |
| --- | --- | --- |
| `light` | `toggle` | `light.toggle` |
| `light` | `turnOn` | `light.turn_on` |
| `light` | `turnOff` | `light.turn_off` |
| `light` | `brightness` | `light.turn_on` with `brightness` |
| `switch` | `toggle` | `switch.toggle` |
| `switch` | `turnOn` | `switch.turn_on` |
| `switch` | `turnOff` | `switch.turn_off` |
| `climate` | `setTemperature` | `climate.set_temperature` with `temperature` |
| `cover` | `open` | `cover.open_cover` |
| `cover` | `close` | `cover.close_cover` |
| `cover` | `stop` | `cover.stop_cover` |
| `cover` | `position` | `cover.set_cover_position` with `position` |
| `scene` | `activate` | `scene.turn_on` |
| `script` | `run` | `script.turn_on` |
| `media_player` | `turnOn` | `media_player.turn_on` |
| `media_player` | `turnOff` | `media_player.turn_off` |
| `media_player` | `play` | `media_player.media_play` |
| `media_player` | `pause` | `media_player.media_pause` |
| `media_player` | `volume` | `media_player.volume_set` with `volume_level` |

## Validation
Unsupported domains/actions return `400 Bad Request`. Numeric values are required for numeric actions and are range-checked:

- `cover.position`: `0..100`
- `light.brightness`: `0..255`
- `media_player.volume`: `0..1`

Home Assistant configuration, network, authorization, and timeout failures reuse the existing widget-catalog response semantics.
