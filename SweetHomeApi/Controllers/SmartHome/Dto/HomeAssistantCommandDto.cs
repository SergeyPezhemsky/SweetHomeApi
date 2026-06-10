using System.Text.Json;

namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class HomeAssistantCommandDto
{
    public required string Action { get; set; }

    public JsonElement? Value { get; set; }
}
