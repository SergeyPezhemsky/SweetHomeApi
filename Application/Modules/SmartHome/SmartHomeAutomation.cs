using Application.Modules.Auth;

namespace Application.Modules.SmartHome;

public class SmartHomeAutomation
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required bool Enabled { get; set; }

    public required string TriggerJson { get; set; }

    public required string ConditionsJson { get; set; }

    public required string ActionsJson { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required DateTime UpdatedAt { get; set; }

    public DateTime? LastExecutedAt { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }
}
