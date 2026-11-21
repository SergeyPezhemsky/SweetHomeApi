using Application.Modules.Auth;

namespace Application.Modules.SmartHome;

public class SmartHomeRoom
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Icon { get; set; }

    public required int Order { get; set; }

    public required bool Hide { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }
}
