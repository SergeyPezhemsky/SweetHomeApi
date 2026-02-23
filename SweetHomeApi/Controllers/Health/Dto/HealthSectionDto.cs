namespace SweetHomeApi.Controllers.Health.Dto;

public class HealthSectionDto
{
    public required string Id { get; set; }
    public required int Order { get; set; }
    public required string Name { get; set; }
    public required bool Hide { get; set; }
    public required string Type { get; set; }
    public required bool Dictionary { get; set; }
    public string? DefaultValue { get; set; }
}
