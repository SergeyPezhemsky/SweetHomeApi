using System.Text.Json;

namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class SmartHomeAutomationDto
{
    public string? Id { get; set; }

    public required string Name { get; set; }

    public required bool Enabled { get; set; }

    public JsonElement? Trigger { get; set; }

    public JsonElement? Conditions { get; set; }

    public JsonElement? Actions { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastExecutedAt { get; set; }
}
