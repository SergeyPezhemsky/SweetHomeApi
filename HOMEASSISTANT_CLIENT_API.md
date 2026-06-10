# Home Assistant Client API

Документ описывает API, нужное клиенту для реализации экрана умного дома: получение каталога сущностей Home Assistant, сохранение комнат и пользовательских виджетов, получение сохраненной раскладки.

## Общие правила

- Base URL: тот же backend SweetHomeApi.
- Авторизация: cookie-based ASP.NET Identity.
- Для браузерных запросов обязательно отправлять cookies:

```ts
fetch(url, {
  credentials: "include"
});
```

- Все endpoints ниже требуют авторизацию.
- Неавторизованный пользователь получает `401 Unauthorized`.
- JSON использует `camelCase`.
- `id` комнат и сохраненных виджетов генерирует клиент. Рекомендуется использовать UUID.

## Поток клиента

1. Войти или зарегистрироваться через существующий auth API.
2. Вызвать `GET /api/SmartHome/widget-catalog`, чтобы получить доступные сущности Home Assistant.
3. Дать пользователю выбрать сущности и создать из них локальные виджеты.
4. Сохранить комнаты и виджеты через `PUT /api/SmartHome/layout`.
5. При открытии экрана получать сохраненную раскладку через `GET /api/SmartHome/layout`.

## GET /api/SmartHome/widget-catalog

Возвращает live-каталог поддерживаемых сущностей из Home Assistant. Эти данные не являются пользовательской раскладкой, а только источником для выбора виджетов.

### Response 200

```json
[
  {
    "id": "light.living_room_main",
    "type": "light",
    "name": "Living Room Main",
    "icon": "lightbulb",
    "source": "homeAssistant",
    "unit": null,
    "state": "on",
    "lastChanged": "2026-06-10T08:20:15.123456+00:00",
    "lastUpdated": "2026-06-10T08:25:40.123456+00:00",
    "capabilities": ["turnOn", "turnOff", "toggle", "brightness", "color"],
    "attributes": {
      "friendly_name": "Living Room Main",
      "supported_color_modes": ["brightness", "color_temp"]
    }
  }
]
```

### Catalog widget fields

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | Home Assistant `entity_id`. Используется как `entityId` при создании сохраненного виджета. |
| `type` | string | Домен Home Assistant: `light`, `switch`, `sensor`, `binary_sensor`, `climate`, `cover`, `scene`, `script`, `media_player`. |
| `name` | string | Человекочитаемое имя из `friendly_name` или fallback на `entity_id`. |
| `icon` | string | Иконка из HA attribute `icon` или дефолт backend. |
| `source` | string | Сейчас всегда `homeAssistant`. |
| `unit` | string или null | Единица измерения для sensor-like сущностей. |
| `state` | string | Текущее состояние HA entity. |
| `lastChanged` | string | ISO datetime изменения состояния. |
| `lastUpdated` | string | ISO datetime последнего обновления. |
| `capabilities` | string[] | Поддерживаемые действия для будущего управления. |
| `attributes` | object | Raw attributes из Home Assistant. |

### Error responses

| Status | When |
| --- | --- |
| `401` | Пользователь не авторизован. |
| `502` | Home Assistant недоступен или запрос к нему завершился ошибкой. |
| `503` | Интеграция Home Assistant не настроена. |
| `504` | Timeout запроса к Home Assistant. |

## GET /api/SmartHome/layout

Возвращает сохраненные комнаты и пользовательские smart-home виджеты текущего пользователя. Данные другого пользователя не возвращаются.

### Response 200

```json
{
  "rooms": [
    {
      "id": "8f79b2d7-2d22-4d26-872a-f76fd94a981e",
      "name": "Гостиная",
      "icon": "sofa",
      "order": 1,
      "hide": false
    }
  ],
  "widgets": [
    {
      "id": "a7aef6f8-bce9-40e8-a996-bfce30b5dbb5",
      "entityId": "light.living_room_main",
      "type": "light",
      "name": "Главный свет",
      "icon": "lightbulb",
      "order": 1,
      "size": 1,
      "hide": false,
      "roomId": "8f79b2d7-2d22-4d26-872a-f76fd94a981e",
      "settingsJson": "{\"accentColor\":\"#ffd166\"}"
    }
  ]
}
```

Если раскладка еще не сохранена, backend возвращает пустые массивы:

```json
{
  "rooms": [],
  "widgets": []
}
```

## PUT /api/SmartHome/layout

Полностью заменяет сохраненную smart-home раскладку текущего пользователя. Клиент должен отправлять полный актуальный список комнат и виджетов.

### Request body

```json
{
  "rooms": [
    {
      "id": "8f79b2d7-2d22-4d26-872a-f76fd94a981e",
      "name": "Гостиная",
      "icon": "sofa",
      "order": 1,
      "hide": false
    },
    {
      "id": "3d4c76be-3c07-418d-bd7f-cf5bbd551d02",
      "name": "Кухня",
      "icon": "utensils",
      "order": 2,
      "hide": false
    }
  ],
  "widgets": [
    {
      "id": "a7aef6f8-bce9-40e8-a996-bfce30b5dbb5",
      "entityId": "light.living_room_main",
      "type": "light",
      "name": "Главный свет",
      "icon": "lightbulb",
      "order": 1,
      "size": 1,
      "hide": false,
      "roomId": "8f79b2d7-2d22-4d26-872a-f76fd94a981e",
      "settingsJson": "{\"accentColor\":\"#ffd166\"}"
    },
    {
      "id": "3afa7bf4-cce0-48f9-9c9d-9f7444b37a88",
      "entityId": "sensor.kitchen_temperature",
      "type": "sensor",
      "name": "Температура",
      "icon": "thermometer",
      "order": 2,
      "size": 1,
      "hide": false,
      "roomId": "3d4c76be-3c07-418d-bd7f-cf5bbd551d02",
      "settingsJson": "{}"
    }
  ]
}
```

### Response 204

Успешно сохранено, body отсутствует.

### Replace behavior

- Backend удаляет старые комнаты и smart-home виджеты текущего пользователя.
- Затем сохраняет переданные `rooms` и `widgets`.
- Если `widget.roomId` указывает на комнату, которой нет в текущем request body, backend сохранит `roomId: null`.
- `settingsJson` можно не передавать или передать пустым, тогда backend сохранит `{}`.
- Для удаления комнаты или виджета нужно отправить layout без этого объекта.
- Для изменения порядка нужно обновить `order` и отправить полный layout.

## DTO Reference

### SmartHomeLayout

| Field | Type | Required |
| --- | --- | --- |
| `rooms` | `SmartHomeRoom[]` | yes |
| `widgets` | `SmartHomeWidget[]` | yes |

### SmartHomeRoom

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `id` | string | yes | Stable client-generated id. |
| `name` | string | yes | Display name. |
| `icon` | string | yes | Client icon id, for example lucide/material alias. |
| `order` | number | yes | Sort order inside room list. |
| `hide` | boolean | yes | Hidden state. |

### SmartHomeWidget

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `id` | string | yes | Stable client-generated widget id. |
| `entityId` | string | yes | Home Assistant entity id from catalog `id`. |
| `type` | string | yes | Usually copied from catalog `type`. |
| `name` | string | yes | User-facing widget name; can be edited by user. |
| `icon` | string | yes | Widget icon; can be copied from catalog and edited by user. |
| `order` | number | yes | Sort order in the UI. |
| `size` | number | yes | Client-defined tile size. Backend stores the number as-is. |
| `hide` | boolean | yes | Hidden state. |
| `roomId` | string или null | no | Must match one of `rooms[].id` to bind widget to a room. |
| `settingsJson` | string | no | JSON string for client-specific widget settings. |

## TypeScript Types

```ts
export type SmartHomeLayout = {
  rooms: SmartHomeRoom[];
  widgets: SmartHomeWidget[];
};

export type SmartHomeRoom = {
  id: string;
  name: string;
  icon: string;
  order: number;
  hide: boolean;
};

export type SmartHomeWidget = {
  id: string;
  entityId: string;
  type: string;
  name: string;
  icon: string;
  order: number;
  size: number;
  hide: boolean;
  roomId?: string | null;
  settingsJson?: string | null;
};

export type HomeAssistantCatalogWidget = {
  id: string;
  type: string;
  name: string;
  icon: string;
  source: "homeAssistant";
  unit: string | null;
  state: string;
  lastChanged: string;
  lastUpdated: string;
  capabilities: string[];
  attributes: Record<string, unknown>;
};
```

## Frontend helper examples

```ts
const apiBaseUrl = "https://example.com";

export async function getHomeAssistantCatalog(): Promise<HomeAssistantCatalogWidget[]> {
  const response = await fetch(`${apiBaseUrl}/api/SmartHome/widget-catalog`, {
    credentials: "include"
  });

  if (!response.ok) {
    throw new Error(`Failed to load Home Assistant catalog: ${response.status}`);
  }

  return response.json();
}

export async function getSmartHomeLayout(): Promise<SmartHomeLayout> {
  const response = await fetch(`${apiBaseUrl}/api/SmartHome/layout`, {
    credentials: "include"
  });

  if (!response.ok) {
    throw new Error(`Failed to load smart-home layout: ${response.status}`);
  }

  return response.json();
}

export async function saveSmartHomeLayout(layout: SmartHomeLayout): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/SmartHome/layout`, {
    method: "PUT",
    credentials: "include",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(layout)
  });

  if (!response.ok) {
    throw new Error(`Failed to save smart-home layout: ${response.status}`);
  }
}
```

## Current limitations

- API сохраняет выбранные виджеты и комнаты, но пока не выполняет команды управления Home Assistant entity.
- `settingsJson` не валидируется backend-ом как JSON-объект, это строковое поле для клиента.
- Backend не проверяет, что `entityId` реально существует в Home Assistant при сохранении layout.
- Получение live-состояний идет только через `widget-catalog`; сохраненный layout хранит последнюю пользовательскую конфигурацию, а не live-state.
