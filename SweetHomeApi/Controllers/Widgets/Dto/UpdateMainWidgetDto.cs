namespace SweetHomeApi.Controllers.Widgets.Dto;

public class UpdateMainWidgetDto
{
    public required string Id { get; set; }
    public required string Alias { get; set; }
    public required int Order { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required int Size { get; set; }
    public required bool Hide { get; set; }
}
