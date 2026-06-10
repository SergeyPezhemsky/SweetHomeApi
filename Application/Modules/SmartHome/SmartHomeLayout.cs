namespace Application.Modules.SmartHome;

public class SmartHomeLayout
{
    public required List<SmartHomeRoom> Rooms { get; set; }

    public required List<SmartHomeWidget> Widgets { get; set; }
}
