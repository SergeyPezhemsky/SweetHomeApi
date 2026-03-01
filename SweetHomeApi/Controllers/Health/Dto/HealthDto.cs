using System.Text.Json.Serialization;

namespace SweetHomeApi.Controllers.Health.Dto;

public class HealthDto
{
    [JsonPropertyName("date")]
    public required string Date { get; set; }

    public required List<HealthDictionaryItemDto> HealthDictionary { get; set; }
    public string? Weight { get; set; }
    public string? BloodPressure { get; set; }
    public string? BloodSugar { get; set; }
    public string? Water { get; set; }
    public string? Temperature { get; set; }
    public bool Monthlies { get; set; }
}
