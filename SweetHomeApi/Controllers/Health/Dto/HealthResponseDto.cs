namespace SweetHomeApi.Controllers.Health.Dto;

public class HealthResponseDto
{
    public required List<HealthSectionDto> HealthSections { get; set; }
    public required HealthDto Health { get; set; }
}
