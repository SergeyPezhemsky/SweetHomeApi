namespace Application.Modules.HomeAssistant;

public class HomeAssistantWidgetControl
{
    public required string Type { get; set; }

    public required string Action { get; set; }

    public required string Label { get; set; }

    public double? Min { get; set; }

    public double? Max { get; set; }

    public double? Step { get; set; }

    public string? Unit { get; set; }
}
