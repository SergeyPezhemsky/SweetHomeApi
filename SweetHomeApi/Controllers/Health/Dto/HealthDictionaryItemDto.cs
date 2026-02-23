using System.Text.Json.Serialization;

namespace SweetHomeApi.Controllers.Health.Dto;

public class HealthDictionaryItemDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string HealthSection { get; set; }
    public bool Value { get; set; }
}
