using System.Text.Json;

namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class SmartHomeScenarioDto
{
    public string? Id { get; set; }

    public required string Name { get; set; }

    public string? Icon { get; set; }

    public JsonElement? Actions { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
