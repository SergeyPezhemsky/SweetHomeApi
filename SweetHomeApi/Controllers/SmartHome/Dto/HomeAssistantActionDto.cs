using System.Text.Json;

namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class HomeAssistantActionDto
{
    public required string EntityId { get; set; }

    public required string Action { get; set; }

    public JsonElement? Value { get; set; }
}
