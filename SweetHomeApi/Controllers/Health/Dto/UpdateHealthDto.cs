using System.Text.Json.Serialization;

namespace SweetHomeApi.Controllers.Health.Dto;

public class UpdateHealthDto
{
    [JsonPropertyName("data")]
    public required string Data { get; set; }

    public List<HealthDictionaryItemDto>? HealthDictionary { get; set; }
    public string? Weight { get; set; }
    public string? BloodPressure { get; set; }
    public string? BloodSugar { get; set; }
    public string? Water { get; set; }
    public string? Temperature { get; set; }
    public bool? Monthlies { get; set; }
}
