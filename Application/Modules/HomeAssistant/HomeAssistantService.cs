using System.Globalization;
using System.Text.Json;

namespace Application.Modules.HomeAssistant;

public class HomeAssistantService(IHomeAssistantClient homeAssistantClient) : IHomeAssistantService
{
    private static readonly HashSet<string> SupportedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "light",
        "switch",
        "sensor",
        "binary_sensor",
        "climate",
        "cover",
        "scene",
        "script",
        "media_player"
    };

    public async Task<IReadOnlyList<HomeAssistantCatalogWidget>> GetWidgetCatalogAsync(CancellationToken cancellationToken)
    {
        var states = await homeAssistantClient.GetStatesAsync(cancellationToken);

        return states
            .Select(MapToCatalogWidget)
            .Where(widget => widget is not null)
            .Select(widget => widget!)
            .OrderBy(widget => widget.Type)
            .ThenBy(widget => widget.Name)
            .ToList();
    }

    public Task ExecuteActionAsync(HomeAssistantActionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EntityId))
            throw new HomeAssistantActionException("EntityId is required.");

        if (string.IsNullOrWhiteSpace(request.Action))
            throw new HomeAssistantActionException("Action is required.");

        var domain = GetDomain(request.EntityId);
        var call = CreateServiceCall(domain, request);

        return homeAssistantClient.CallServiceAsync(call.Domain, call.Service, call.Data, cancellationToken);
    }

    private static HomeAssistantCatalogWidget? MapToCatalogWidget(HomeAssistantEntityState state)
    {
        var domain = GetDomain(state.EntityId);
        if (!SupportedDomains.Contains(domain))
            return null;

        return new HomeAssistantCatalogWidget
        {
            Id = state.EntityId,
            Type = domain,
            Name = GetFriendlyName(state),
            Icon = GetIcon(domain, state.Attributes),
            Source = "homeAssistant",
            DisplayType = GetDisplayType(domain),
            Unit = GetStringAttribute(state.Attributes, "unit_of_measurement"),
            State = state.State,
            LastChanged = state.LastChanged,
            LastUpdated = state.LastUpdated,
            Capabilities = GetCapabilities(domain, state.Attributes),
            Controls = GetControls(domain, state.Attributes),
            Attributes = state.Attributes
        };
    }

    private static string GetDomain(string entityId)
    {
        var separatorIndex = entityId.IndexOf('.');
        return separatorIndex <= 0 ? entityId : entityId[..separatorIndex];
    }

    private static string GetFriendlyName(HomeAssistantEntityState state)
    {
        return GetStringAttribute(state.Attributes, "friendly_name") ?? state.EntityId;
    }

    private static string GetIcon(string domain, IReadOnlyDictionary<string, JsonElement> attributes)
    {
        var customIcon = GetStringAttribute(attributes, "icon");
        if (!string.IsNullOrWhiteSpace(customIcon))
            return customIcon;

        return domain switch
        {
            "light" => "lightbulb",
            "switch" => "toggle-right",
            "sensor" => "activity",
            "binary_sensor" => "circle-dot",
            "climate" => "thermometer",
            "cover" => "blinds",
            "scene" => "panel-top",
            "script" => "scroll-text",
            "media_player" => "speaker",
            _ => "box"
        };
    }

    private static string GetDisplayType(string domain)
    {
        return domain switch
        {
            "light" => "toggleSlider",
            "switch" => "toggle",
            "sensor" => "value",
            "binary_sensor" => "status",
            "climate" => "thermostat",
            "cover" => "cover",
            "scene" => "actionButton",
            "script" => "actionButton",
            "media_player" => "mediaControls",
            _ => "value"
        };
    }

    private static List<string> GetCapabilities(string domain, IReadOnlyDictionary<string, JsonElement> attributes)
    {
        var capabilities = domain switch
        {
            "light" => new List<string> { "turnOn", "turnOff", "toggle" },
            "switch" => new List<string> { "turnOn", "turnOff", "toggle" },
            "climate" => new List<string> { "setTemperature" },
            "cover" => new List<string> { "open", "close", "stop" },
            "scene" => new List<string> { "activate" },
            "script" => new List<string> { "run" },
            "media_player" => new List<string> { "turnOn", "turnOff", "play", "pause", "volume" },
            _ => new List<string>()
        };

        if (domain == "light")
        {
            AddLightCapabilities(capabilities, attributes);
        }
        else if (domain == "cover")
        {
            AddCoverCapabilities(capabilities, attributes);
        }

        return capabilities;
    }

    private static List<HomeAssistantWidgetControl> GetControls(
        string domain,
        IReadOnlyDictionary<string, JsonElement> attributes)
    {
        var controls = domain switch
        {
            "light" => new List<HomeAssistantWidgetControl>
            {
                new() { Type = "toggle", Action = "toggle", Label = "Toggle" }
            },
            "switch" => new List<HomeAssistantWidgetControl>
            {
                new() { Type = "toggle", Action = "toggle", Label = "Toggle" }
            },
            "climate" => new List<HomeAssistantWidgetControl>
            {
                new()
                {
                    Type = "stepper",
                    Action = "setTemperature",
                    Label = "Temperature",
                    Min = GetDoubleAttribute(attributes, "min_temp"),
                    Max = GetDoubleAttribute(attributes, "max_temp"),
                    Step = GetDoubleAttribute(attributes, "target_temp_step") ?? 0.5,
                    Unit = GetStringAttribute(attributes, "temperature_unit")
                }
            },
            "cover" => new List<HomeAssistantWidgetControl>
            {
                new() { Type = "button", Action = "open", Label = "Open" },
                new() { Type = "button", Action = "close", Label = "Close" },
                new() { Type = "button", Action = "stop", Label = "Stop" }
            },
            "scene" => new List<HomeAssistantWidgetControl>
            {
                new() { Type = "button", Action = "activate", Label = "Activate" }
            },
            "script" => new List<HomeAssistantWidgetControl>
            {
                new() { Type = "button", Action = "run", Label = "Run" }
            },
            "media_player" => new List<HomeAssistantWidgetControl>
            {
                new() { Type = "button", Action = "turnOn", Label = "On" },
                new() { Type = "button", Action = "turnOff", Label = "Off" },
                new() { Type = "button", Action = "play", Label = "Play" },
                new() { Type = "button", Action = "pause", Label = "Pause" },
                new() { Type = "slider", Action = "volume", Label = "Volume", Min = 0, Max = 1, Step = 0.01 }
            },
            _ => new List<HomeAssistantWidgetControl>()
        };

        if (domain == "light")
        {
            AddLightControls(controls, attributes);
        }
        else if (domain == "cover")
        {
            AddCoverControls(controls, attributes);
        }

        return controls;
    }

    private static HomeAssistantServiceCall CreateServiceCall(string domain, HomeAssistantActionRequest request)
    {
        var action = request.Action.Trim();
        var data = new Dictionary<string, object?>
        {
            ["entity_id"] = request.EntityId.Trim()
        };

        var service = domain switch
        {
            "light" => MapLightAction(action, request.Value, data),
            "switch" => MapSwitchAction(action),
            "climate" => MapClimateAction(action, request.Value, data),
            "cover" => MapCoverAction(action, request.Value, data),
            "scene" => MapSceneAction(action),
            "script" => MapScriptAction(action),
            "media_player" => MapMediaPlayerAction(action, request.Value, data),
            _ => throw new HomeAssistantActionException($"Unsupported Home Assistant domain '{domain}'.")
        };

        return new HomeAssistantServiceCall(domain, service, data);
    }

    private static string MapLightAction(
        string action,
        JsonElement? value,
        Dictionary<string, object?> data)
    {
        return action switch
        {
            "toggle" => "toggle",
            "turnOn" => "turn_on",
            "turnOff" => "turn_off",
            "brightness" => AddNumber(data, "brightness", value, 0, 255, "Brightness is required."),
            _ => throw new HomeAssistantActionException($"Unsupported light action '{action}'.")
        };
    }

    private static string MapSwitchAction(string action)
    {
        return action switch
        {
            "toggle" => "toggle",
            "turnOn" => "turn_on",
            "turnOff" => "turn_off",
            _ => throw new HomeAssistantActionException($"Unsupported switch action '{action}'.")
        };
    }

    private static string MapClimateAction(
        string action,
        JsonElement? value,
        Dictionary<string, object?> data)
    {
        return action switch
        {
            "setTemperature" => AddNumber(data, "temperature", value, null, null, "Temperature is required."),
            _ => throw new HomeAssistantActionException($"Unsupported climate action '{action}'.")
        };
    }

    private static string MapCoverAction(
        string action,
        JsonElement? value,
        Dictionary<string, object?> data)
    {
        return action switch
        {
            "open" => "open_cover",
            "close" => "close_cover",
            "stop" => "stop_cover",
            "position" => AddNumber(data, "position", value, 0, 100, "Position percent is required."),
            _ => throw new HomeAssistantActionException($"Unsupported cover action '{action}'.")
        };
    }

    private static string MapSceneAction(string action)
    {
        return action switch
        {
            "activate" => "turn_on",
            _ => throw new HomeAssistantActionException($"Unsupported scene action '{action}'.")
        };
    }

    private static string MapScriptAction(string action)
    {
        return action switch
        {
            "run" => "turn_on",
            _ => throw new HomeAssistantActionException($"Unsupported script action '{action}'.")
        };
    }

    private static string MapMediaPlayerAction(
        string action,
        JsonElement? value,
        Dictionary<string, object?> data)
    {
        return action switch
        {
            "turnOn" => "turn_on",
            "turnOff" => "turn_off",
            "play" => "media_play",
            "pause" => "media_pause",
            "volume" => AddNumber(data, "volume_level", value, 0, 1, "Volume level is required."),
            _ => throw new HomeAssistantActionException($"Unsupported media_player action '{action}'.")
        };
    }

    private static string AddNumber(
        Dictionary<string, object?> data,
        string field,
        JsonElement? value,
        double? min,
        double? max,
        string requiredMessage)
    {
        if (value is null)
            throw new HomeAssistantActionException(requiredMessage);

        double number;
        if (value.Value.ValueKind == JsonValueKind.Number)
        {
            if (!value.Value.TryGetDouble(out number))
                throw new HomeAssistantActionException($"{field} must be a number.");
        }
        else if (value.Value.ValueKind == JsonValueKind.String
                 && double.TryParse(
                     value.Value.GetString(),
                     NumberStyles.Float,
                     CultureInfo.InvariantCulture,
                     out var parsedNumber))
        {
            number = parsedNumber;
        }
        else
        {
            throw new HomeAssistantActionException($"{field} must be a number.");
        }

        if (min is not null && number < min)
            throw new HomeAssistantActionException($"{field} must be greater than or equal to {min}.");

        if (max is not null && number > max)
            throw new HomeAssistantActionException($"{field} must be less than or equal to {max}.");

        data[field] = number;
        return field switch
        {
            "brightness" => "turn_on",
            "temperature" => "set_temperature",
            "position" => "set_cover_position",
            "volume_level" => "volume_set",
            _ => throw new HomeAssistantActionException($"Unsupported numeric field '{field}'.")
        };
    }

    private static void AddLightControls(
        List<HomeAssistantWidgetControl> controls,
        IReadOnlyDictionary<string, JsonElement> attributes)
    {
        var supportedColorModes = GetStringArrayAttribute(attributes, "supported_color_modes");

        if (supportedColorModes.Any(mode => mode is "brightness" or "color_temp" or "hs" or "rgb" or "rgbw" or "rgbww" or "xy"))
        {
            controls.Add(new()
            {
                Type = "slider",
                Action = "brightness",
                Label = "Brightness",
                Min = 0,
                Max = 255,
                Step = 1
            });
        }

        if (supportedColorModes.Any(mode => mode is "color_temp" or "hs" or "rgb" or "rgbw" or "rgbww" or "xy"))
        {
            controls.Add(new()
            {
                Type = "colorPicker",
                Action = "color",
                Label = "Color"
            });
        }
    }

    private static void AddCoverControls(
        List<HomeAssistantWidgetControl> controls,
        IReadOnlyDictionary<string, JsonElement> attributes)
    {
        if (!attributes.ContainsKey("current_position"))
            return;

        controls.Add(new()
        {
            Type = "slider",
            Action = "position",
            Label = "Position",
            Min = 0,
            Max = 100,
            Step = 1,
            Unit = "%"
        });
    }

    private static void AddLightCapabilities(List<string> capabilities, IReadOnlyDictionary<string, JsonElement> attributes)
    {
        var supportedColorModes = GetStringArrayAttribute(attributes, "supported_color_modes");
        if (supportedColorModes.Any(mode => mode is "brightness" or "color_temp" or "hs" or "rgb" or "rgbw" or "rgbww" or "xy"))
        {
            capabilities.Add("brightness");
        }

        if (supportedColorModes.Any(mode => mode is "color_temp" or "hs" or "rgb" or "rgbw" or "rgbww" or "xy"))
        {
            capabilities.Add("color");
        }
    }

    private static void AddCoverCapabilities(List<string> capabilities, IReadOnlyDictionary<string, JsonElement> attributes)
    {
        if (attributes.ContainsKey("current_position"))
        {
            capabilities.Add("position");
        }
    }

    private static string? GetStringAttribute(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!attributes.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }

    private static double? GetDoubleAttribute(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!attributes.TryGetValue(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;

        return null;
    }

    private static IReadOnlyList<string> GetStringArrayAttribute(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
    {
        if (!attributes.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private sealed record HomeAssistantServiceCall(
        string Domain,
        string Service,
        IReadOnlyDictionary<string, object?> Data);
}
