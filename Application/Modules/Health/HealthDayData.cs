namespace Application.Modules.Health;

public class HealthDayData
{
    public DateOnly Date { get; set; }
    public List<HealthDictionaryItem> HealthDictionary { get; set; } = [];

    public string? Weight { get; set; }
    public string? BloodPressure { get; set; }
    public string? BloodSugar { get; set; }
    public string? Water { get; set; }
    public string? Temperature { get; set; }
    public bool Monthlies { get; set; }
}
