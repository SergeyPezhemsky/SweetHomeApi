using System.Text.Json;

namespace Application.Modules.HomeAssistant;

public class HomeAssistantCatalogWidget
{
    public required string Id { get; set; }

    public required string Type { get; set; }

    public required string Name { get; set; }

    public required string Icon { get; set; }

    public required string Source { get; set; }

    public string? Unit { get; set; }

    public required string State { get; set; }

    public required DateTimeOffset LastChanged { get; set; }

    public required DateTimeOffset LastUpdated { get; set; }

    public required List<string> Capabilities { get; set; }

    public Dictionary<string, JsonElement> Attributes { get; set; } = new();
}
