using Microsoft.AspNetCore.Identity;

namespace Application.Modules.SmartHome;

public class SmartHomeWidget
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

    public required string SettingsJson { get; set; }

    public required string UserId { get; set; }

    public IdentityUser? User { get; set; }

    public SmartHomeRoom? Room { get; set; }
}
