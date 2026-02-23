using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Health;

public class HealthEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateOnly Date { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IdentityUser? User { get; set; }

    public string? Weight { get; set; }
    public string? BloodPressure { get; set; }
    public string? BloodSugar { get; set; }
    public string? Water { get; set; }
    public string? Temperature { get; set; }
    public bool Monthlies { get; set; }

    // JSON словарь значений по id пункта справочника: { "calm": true, ... }
    public string DictionaryStateJson { get; set; } = "{}";
}
