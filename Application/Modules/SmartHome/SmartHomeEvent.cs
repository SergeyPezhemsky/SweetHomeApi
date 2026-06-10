using Microsoft.AspNetCore.Identity;

namespace Application.Modules.SmartHome;

public class SmartHomeEvent
{
    public required string Id { get; set; }

    public required string Type { get; set; }

    public required string Title { get; set; }

    public required string Message { get; set; }

    public string? EntityId { get; set; }

    public string? RoomId { get; set; }

    public required string PayloadJson { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required string UserId { get; set; }

    public IdentityUser? User { get; set; }
}
