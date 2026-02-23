namespace Application.Modules.Health;

public interface IHealthService
{
    Task<List<HealthSection>> GetSectionsAsync();
    Task<HealthDayData> GetByDateAsync(string userId, DateOnly date);
    Task UpsertAsync(string userId, HealthDayData dayData);
}
