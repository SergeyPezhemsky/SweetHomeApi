using System.Text.Json;

namespace Application.Modules.HomeAssistant;

public class HomeAssistantEntityState
{
    public required string EntityId { get; set; }

    public required string State { get; set; }

    public required DateTimeOffset LastChanged { get; set; }

    public required DateTimeOffset LastUpdated { get; set; }

    public Dictionary<string, JsonElement> Attributes { get; set; } = new();
}
