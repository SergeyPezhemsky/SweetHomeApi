namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class SmartHomeLayoutDto
{
    public required List<SmartHomeRoomDto> Rooms { get; set; }

    public required List<SmartHomeWidgetDto> Widgets { get; set; }
}
