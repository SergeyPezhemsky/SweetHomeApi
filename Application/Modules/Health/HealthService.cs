using System.Text.Json;
using Application.Modules.Health.Seeds;

namespace Application.Modules.Health;

public class HealthService(IHealthRepository healthRepository) : IHealthService
{
    public Task<List<HealthSection>> GetSectionsAsync()
    {
        return Task.FromResult(DefaultHealthConfiguration.GetSections());
    }

    public async Task<HealthDayData> GetByDateAsync(string userId, DateOnly date)
    {
        var entry = await healthRepository.GetByUserIdAndDateAsync(userId, date);
        var dictionaryValues = DeserializeDictionaryState(entry?.DictionaryStateJson);

        var healthDictionary = DefaultHealthConfiguration.GetDictionary()
            .Select(item => new HealthDictionaryItem
            {
                Id = item.Id,
                Name = item.Name,
                HealthSection = item.HealthSection,
                Value = dictionaryValues.TryGetValue(item.Id, out var value) && value
            })
            .ToList();

        return new HealthDayData
        {
            Date = date,
            HealthDictionary = healthDictionary,
            Weight = entry?.Weight,
            BloodPressure = entry?.BloodPressure,
            BloodSugar = entry?.BloodSugar,
            Water = entry?.Water,
            Temperature = entry?.Temperature,
            Monthlies = entry?.Monthlies ?? false
        };
    }

    public async Task UpsertAsync(string userId, HealthDayData dayData)
    {
        var dictionaryState = dayData.HealthDictionary.ToDictionary(x => x.Id, x => x.Value);

        var entry = new HealthEntry
        {
            Date = dayData.Date,
            UserId = userId,
            Weight = dayData.Weight,
            BloodPressure = dayData.BloodPressure,
            BloodSugar = dayData.BloodSugar,
            Water = dayData.Water,
            Temperature = dayData.Temperature,
            Monthlies = dayData.Monthlies,
            DictionaryStateJson = JsonSerializer.Serialize(dictionaryState)
        };

        await healthRepository.UpsertAsync(entry);
    }

    private static Dictionary<string, bool> DeserializeDictionaryState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, bool>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
        }
        catch
        {
            return new Dictionary<string, bool>();
        }
    }
}
