using System.Text.Json;

namespace SweetHomeApi.Controllers.SmartHome.Dto;

public class SmartHomeEventDto
{
    public required string Id { get; set; }

    public required string Type { get; set; }

    public required string Title { get; set; }

    public required string Message { get; set; }

    public string? EntityId { get; set; }

    public string? RoomId { get; set; }

    public JsonElement Payload { get; set; }

    public required DateTime CreatedAt { get; set; }
}
