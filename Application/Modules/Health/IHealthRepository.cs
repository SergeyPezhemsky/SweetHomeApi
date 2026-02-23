namespace Application.Modules.Health;

public interface IHealthRepository
{
    Task<HealthEntry?> GetByUserIdAndDateAsync(string userId, DateOnly date);
    Task UpsertAsync(HealthEntry entry);
}
