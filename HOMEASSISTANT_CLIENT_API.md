# Home Assistant Client API

Документ описывает API для клиентского экрана умного дома: каталог сущностей Home Assistant, сохранение комнат, сохранение пользовательских виджетов и явный контракт для выбора UI-контролов.

## Общие правила

- Base URL: backend SweetHomeApi.
- Авторизация: cookie-based ASP.NET Identity.
- Для браузерных запросов отправлять cookies:

```ts
fetch(url, {
  credentials: "include"
});
```

- Все endpoints ниже требуют авторизацию.
- Неавторизованный пользователь получает `401 Unauthorized`.
- JSON использует `camelCase`.
- `id` комнат и сохраненных виджетов генерирует клиент. Рекомендуется UUID.

## Клиентский поток

1. Войти или зарегистрироваться через существующий auth API.
2. Вызвать `GET /api/SmartHome/widget-catalog`.
3. Показать пользователю каталог доступных HA-сущностей.
4. Создать локальные виджеты из выбранных catalog items.
5. Сохранить комнаты и виджеты через `PUT /api/SmartHome/layout`.
6. При открытии экрана получить сохраненную раскладку через `GET /api/SmartHome/layout`.

## GET /api/SmartHome/widget-catalog

Возвращает live-каталог поддерживаемых сущностей Home Assistant. Это не сохраненная пользовательская раскладка, а источник для выбора виджетов.

### Response 200

```json
[
  {
    "id": "light.living_room_main",
    "type": "light",
    "name": "Living Room Main",
    "icon": "lightbulb",
    "source": "homeAssistant",
    "displayType": "toggleSlider",
    "unit": null,
    "state": "on",
    "lastChanged": "2026-06-10T08:20:15.123456+00:00",
    "lastUpdated": "2026-06-10T08:25:40.123456+00:00",
    "capabilities": ["turnOn", "turnOff", "toggle", "brightness", "color"],
    "controls": [
      {
        "type": "toggle",
        "action": "toggle",
        "label": "Toggle",
        "min": null,
        "max": null,
        "step": null,
        "unit": null
      },
      {
        "type": "slider",
        "action": "brightness",
        "label": "Brightness",
        "min": 0,
        "max": 255,
        "step": 1,
        "unit": null
      },
      {
        "type": "colorPicker",
        "action": "color",
        "label": "Color",
        "min": null,
        "max": null,
        "step": null,
        "unit": null
      }
    ],
    "attributes": {
      "friendly_name": "Living Room Main",
      "supported_color_modes": ["brightness", "color_temp"]
    }
  }
]
```

### Catalog fields

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | Home Assistant `entity_id`. При сохранении виджета копировать в `entityId`. |
| `type` | string | Домен HA: `light`, `switch`, `sensor`, `binary_sensor`, `climate`, `cover`, `scene`, `script`, `media_player`. |
| `name` | string | Имя из `friendly_name` или fallback на `entity_id`. |
| `icon` | string | Иконка карточки. Может быть HA `mdi:*` или backend alias вроде `lightbulb`. |
| `source` | string | Сейчас всегда `homeAssistant`. |
| `displayType` | string | Основной тип карточки, по нему фронт выбирает layout виджета. |
| `unit` | string или null | Единица измерения для sensor-like сущностей. |
| `state` | string | Текущее состояние HA entity. |
| `lastChanged` | string | ISO datetime изменения состояния. |
| `lastUpdated` | string | ISO datetime последнего обновления. |
| `capabilities` | string[] | Список возможностей для логики/доступности действий. |
| `controls` | `HomeAssistantWidgetControl[]` | Явные UI-контролы, которые фронт должен отрисовать. |
| `attributes` | object | Raw attributes из Home Assistant. |

## Как фронту понять, что рисовать

Фронт не должен угадывать UI только по `type`. Используйте:

- `displayType` для выбора базовой карточки.
- `controls[].type` для выбора конкретного UI-элемента.
- `controls[].action` для команды управления через `POST /api/SmartHome/actions`.
- `icon` для иконки.

### displayType mapping

| `displayType` | Что рисовать |
| --- | --- |
| `toggleSlider` | Карточка устройства с toggle и опциональными slider/color controls. |
| `toggle` | Карточка-переключатель. |
| `value` | Read-only карточка значения. |
| `status` | Read-only карточка статуса. |
| `thermostat` | Термостат с numeric stepper. |
| `cover` | Шторы/ворота: open/close/stop и опциональный процент позиции. |
| `actionButton` | Карточка с основной кнопкой действия. |
| `mediaControls` | Медиа-карточка: кнопки и slider громкости. |

### controls[].type mapping

| `controls[].type` | UI control |
| --- | --- |
| `button` | Кнопка. |
| `toggle` | Switch/toggle. |
| `slider` | Ползунок, использовать `min`, `max`, `step`, `unit`. |
| `colorPicker` | Выбор цвета. |
| `stepper` | Numeric stepper, использовать `min`, `max`, `step`, `unit`. |

### Current backend mapping

| HA type | `displayType` | Controls |
| --- | --- | --- |
| `light` | `toggleSlider` | `toggle`, плюс `slider` для brightness и `colorPicker` для цветных ламп. |
| `switch` | `toggle` | `toggle`. |
| `sensor` | `value` | Нет controls, только read-only значение. |
| `binary_sensor` | `status` | Нет controls, только read-only статус. |
| `climate` | `thermostat` | `stepper` для `setTemperature`. |
| `cover` | `cover` | `button`: `open`, `close`, `stop`; если есть `current_position`, дополнительно `slider`: `position` от 0 до 100 %. |
| `scene` | `actionButton` | `button`: `activate`. |
| `script` | `actionButton` | `button`: `run`. |
| `media_player` | `mediaControls` | `button`: on/off/play/pause, `slider`: volume. |

## POST /api/SmartHome/actions

Выполняет поддерживаемое действие Home Assistant для сущности из smart-home виджета.
Backend не является произвольным proxy к HA services: поддерживаются только действия из `controls[].action`.

### Request body

```json
{
  "entityId": "cover.living_room_curtains",
  "action": "position",
  "value": 45
}
```

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `entityId` | string | yes | Home Assistant entity id, например `cover.living_room_curtains`. |
| `action` | string | yes | Action id из `controls[].action`. |
| `value` | number/string/null | no | Нужно для numeric actions: `position`, `brightness`, `setTemperature`, `volume`. |

### Response 204

Home Assistant принял service call, body отсутствует.

### Error responses

| Status | Meaning |
| --- | --- |
| `400` | Неподдерживаемый domain/action или неверное значение. |
| `502` | Home Assistant вернул ошибку или недоступен. |
| `503` | Интеграция Home Assistant не настроена. |
| `504` | Timeout запроса к Home Assistant. |

### Supported actions

| HA type | Actions |
| --- | --- |
| `light` | `toggle`, `turnOn`, `turnOff`, `brightness` (`value` 0..255). |
| `switch` | `toggle`, `turnOn`, `turnOff`. |
| `climate` | `setTemperature` (`value` number). |
| `cover` | `open`, `close`, `stop`, `position` (`value` 0..100). |
| `scene` | `activate`. |
| `script` | `run`. |
| `media_player` | `turnOn`, `turnOff`, `play`, `pause`, `volume` (`value` 0..1). |

## GET /api/SmartHome/layout

Возвращает сохраненные комнаты и пользовательские smart-home виджеты текущего пользователя.

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

Если раскладка еще не сохранена:

```json
{
  "rooms": [],
  "widgets": []
}
```

Важно: сохраненный layout хранит пользовательскую конфигурацию. Для live-state и UI controls клиент должен сверять `widgets[].entityId` с актуальным `GET /api/SmartHome/widget-catalog`.

## PUT /api/SmartHome/layout

Полностью заменяет сохраненную smart-home раскладку текущего пользователя. Клиент отправляет полный актуальный список комнат и виджетов.

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
    }
  ]
}
```

### Response 204

Успешно сохранено, body отсутствует.

### Replace behavior

- Backend удаляет старые комнаты и smart-home виджеты текущего пользователя.
- Затем сохраняет переданные `rooms` и `widgets`.
- Если `widget.roomId` указывает на комнату, которой нет в request body, backend сохранит `roomId: null`.
- `settingsJson` можно не передавать или передать пустым, тогда backend сохранит `{}`.
- Для удаления комнаты или виджета отправить layout без этого объекта.

## DTO Reference

### HomeAssistantWidgetControl

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `type` | string | yes | `button`, `toggle`, `slider`, `colorPicker`, `stepper`. |
| `action` | string | yes | Action id для будущего endpoint управления. |
| `label` | string | yes | Display label или fallback для aria-label. |
| `min` | number или null | no | Для slider/stepper. |
| `max` | number или null | no | Для slider/stepper. |
| `step` | number или null | no | Для slider/stepper. |
| `unit` | string или null | no | Для отображения значения. |

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
| `icon` | string | yes | Client icon id. |
| `order` | number | yes | Sort order. |
| `hide` | boolean | yes | Hidden state. |

### SmartHomeWidget

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `id` | string | yes | Stable client-generated widget id. |
| `entityId` | string | yes | Home Assistant entity id from catalog `id`. |
| `type` | string | yes | Usually copied from catalog `type`. |
| `name` | string | yes | User-facing widget name. |
| `icon` | string | yes | Widget icon. |
| `order` | number | yes | Sort order. |
| `size` | number | yes | Client-defined tile size. |
| `hide` | boolean | yes | Hidden state. |
| `roomId` | string или null | no | Must match one of `rooms[].id`. |
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

export type HomeAssistantWidgetControl = {
  type: "button" | "toggle" | "slider" | "colorPicker" | "stepper";
  action: string;
  label: string;
  min?: number | null;
  max?: number | null;
  step?: number | null;
  unit?: string | null;
};

export type HomeAssistantCatalogWidget = {
  id: string;
  type: string;
  name: string;
  icon: string;
  source: "homeAssistant";
  displayType:
    | "toggleSlider"
    | "toggle"
    | "value"
    | "status"
    | "thermostat"
    | "cover"
    | "actionButton"
    | "mediaControls";
  unit: string | null;
  state: string;
  lastChanged: string;
  lastUpdated: string;
  capabilities: string[];
  controls: HomeAssistantWidgetControl[];
  attributes: Record<string, unknown>;
};

export type HomeAssistantActionRequest = {
  entityId: string;
  action: string;
  value?: number | string | null;
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

export async function executeHomeAssistantAction(action: HomeAssistantActionRequest): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/SmartHome/actions`, {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(action)
  });

  if (!response.ok) {
    throw new Error(`Failed to execute Home Assistant action: ${response.status}`);
  }
}

await executeHomeAssistantAction({
  entityId: "cover.living_room_curtains",
  action: "position",
  value: 45
});
```

## Current limitations

- Live-state и controls приходят из `widget-catalog`; сохраненный layout хранит пользовательскую конфигурацию.
