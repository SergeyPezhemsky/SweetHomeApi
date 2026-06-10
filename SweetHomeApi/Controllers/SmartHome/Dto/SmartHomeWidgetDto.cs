namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class SmartHomeWidgetDto
{
    public required string Id { get; set; }

    public required string EntityId { get; set; }

    public required string Type { get; set; }

    public required string Name { get; set; }

    public required string Icon { get; set; }

    public required int Order { get; set; }

    public required int Size { get; set; }

    public required bool Hide { get; set; }

    public string? RoomId { get; set; }

    public string? SettingsJson { get; set; }
}
