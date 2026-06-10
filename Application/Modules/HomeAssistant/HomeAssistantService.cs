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

        return controls;
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
}
