using Microsoft.AspNetCore.Identity;

namespace Application.Modules.SmartHome;

public class SmartHomeScenario
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Icon { get; set; }

    public required string ActionsJson { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required DateTime UpdatedAt { get; set; }

    public required string UserId { get; set; }

    public IdentityUser? User { get; set; }
}
