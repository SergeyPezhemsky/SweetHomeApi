using System.Text.Json;

namespace Application.Modules.HomeAssistant;

public class HomeAssistantActionRequest
{
    public required string EntityId { get; set; }

    public required string Action { get; set; }

    public JsonElement? Value { get; set; }
}
