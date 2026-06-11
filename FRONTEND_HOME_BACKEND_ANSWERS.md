# Frontend Home backend answers

Дата проверки: 2026-06-11.

Источник требований frontend: `BACKEND_HOME_GAPS.md` в `PolyaAn/SweetHome` на ветке `master`.

----

## Чего не хватает на frontend

- Подключить экран сценариев к backend API вместо локального/неподдерживаемого состояния.
- Подключить экран создания сценария к backend API вместо локального/неподдерживаемого состояния.
- Подключить экран автоматизаций к backend API вместо локального/неподдерживаемого состояния.
- Подключить экран журнала событий к backend API вместо локального/неподдерживаемого состояния.
- Подключить WebSocket realtime-канал `Дом` и обработать входящие события.

Backend-контракт для этих пунктов уже доступен в `SweetHomeApi`.

----

## Ответы на открытые вопросы

### URL endpoints сценариев

- `GET /api/SmartHome/scenarios` - получить сценарии текущего пользователя.
- `POST /api/SmartHome/scenarios` - создать сценарий.
- `POST /api/SmartHome/scenarios/{scenarioId}/execute` - выполнить сценарий.

Payload создания сценария:

```json
{
  "id": "optional-client-id",
  "name": "Evening mode",
  "icon": "panel-top",
  "actions": [
    {
      "entityId": "light.living_room",
      "action": "turn_on",
      "value": true
    }
  ]
}
```

Ответ `GET` и `POST`: массив или объект `SmartHomeScenario`.

```json
{
  "id": "scenario-id",
  "name": "Evening mode",
  "icon": "panel-top",
  "actions": [],
  "createdAt": "2026-06-11T09:00:00Z",
  "updatedAt": "2026-06-11T09:00:00Z"
}
```

### URL endpoints автоматизаций

- `GET /api/SmartHome/automations` - получить автоматизации текущего пользователя.
- `POST /api/SmartHome/automations` - создать автоматизацию.
- `PUT /api/SmartHome/automations/{automationId}` - обновить автоматизацию.

Payload создания/обновления автоматизации:

```json
{
  "id": "optional-client-id",
  "name": "Turn hallway light on",
  "enabled": true,
  "trigger": {
    "type": "state",
    "entityId": "binary_sensor.motion"
  },
  "conditions": [],
  "actions": [
    {
      "entityId": "light.hallway",
      "action": "turn_on",
      "value": true
    }
  ]
}
```

Ответ `GET`, `POST` и `PUT`: массив или объект `SmartHomeAutomation`.

```json
{
  "id": "automation-id",
  "name": "Turn hallway light on",
  "enabled": true,
  "trigger": {},
  "conditions": [],
  "actions": [],
  "createdAt": "2026-06-11T09:00:00Z",
  "updatedAt": "2026-06-11T09:00:00Z",
  "lastExecutedAt": null
}
```

### URL endpoint журнала событий

- `GET /api/SmartHome/events?take=100` - получить последние события текущего пользователя.

`take` опционален, значение по умолчанию - `100`.

Ответ: массив `SmartHomeEvent`.

```json
{
  "id": "event-id",
  "type": "DEVICE_STATE_CHANGED",
  "title": "Device state changed",
  "message": "Action 'turn_on' was sent to 'light.living_room'.",
  "entityId": "light.living_room",
  "roomId": null,
  "payload": {
    "entityId": "light.living_room",
    "action": "turn_on",
    "value": true
  },
  "createdAt": "2026-06-11T09:00:00Z"
}
```

### URL realtime-канала

- `GET /ws/home` как WebSocket endpoint.

Подключение требует cookie-based авторизацию того же пользователя, что и REST API. Для production frontend URL собирается от текущего origin: `wss://<host>/ws/home`; для локального HTTP - `ws://<host>/ws/home`.

### Формат payload realtime-событий

Backend отправляет текстовое WebSocket-сообщение в JSON:

```json
{
  "type": "DEVICE_STATE_CHANGED",
  "occurredAt": "2026-06-11T09:00:00Z",
  "payload": {
    "id": "event-id",
    "type": "DEVICE_STATE_CHANGED",
    "title": "Device state changed",
    "message": "Action 'turn_on' was sent to 'light.living_room'.",
    "entityId": "light.living_room",
    "roomId": null,
    "payload": {
      "entityId": "light.living_room",
      "action": "turn_on",
      "value": true
    },
    "createdAt": "2026-06-11T09:00:00Z"
  }
}
```

Известные типы событий:

- `DEVICE_STATE_CHANGED` - успешно выполнено действие устройства через `POST /api/SmartHome/actions` или `POST /api/SmartHome/widgets/{entityId}/command`.
- `NEW_EVENT` - выполнен сценарий или обновлена автоматизация.
- `ROOM_UPDATED` - сохранен layout через `PUT /api/SmartHome/layout`.

----

## Дополнительные backend URL для экрана Дом

- `GET /api/SmartHome/widget-catalog` - каталог доступных Home Assistant сущностей для виджетов.
- `GET /api/SmartHome/layout` - получить сохраненную раскладку комнат и виджетов.
- `PUT /api/SmartHome/layout` - заменить раскладку комнат и виджетов.
- `POST /api/SmartHome/actions` - выполнить действие Home Assistant.
- `POST /api/SmartHome/widgets/{entityId}/command` - выполнить команду конкретного виджета.

----

## Валидация, важная для frontend

- `PUT /api/SmartHome/layout` возвращает `400` с `code = ENTITY_ID_REQUIRED`, если у виджета пустой `entityId`.
- `PUT /api/SmartHome/layout` возвращает `400` с `code = INVALID_SETTINGS_JSON`, если `settingsJson` невалидный JSON.
- `PUT /api/SmartHome/layout` возвращает `400` с `code = UNKNOWN_ENTITY_ID`, если layout содержит неизвестные Home Assistant `entityId`.
- Если Home Assistant не настроен, backend возвращает `503`.
- Если Home Assistant недоступен или запрос к нему падает, backend возвращает `502` или `504`.
