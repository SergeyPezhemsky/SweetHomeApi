namespace Application.Modules.Health;

public class HealthDictionaryItem
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string HealthSection { get; set; }
    public bool Value { get; set; }
}
