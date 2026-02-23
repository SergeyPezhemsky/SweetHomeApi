using Application.Modules.Health;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class HealthRepository(SweetHomeDbContext context) : IHealthRepository
{
    public async Task<HealthEntry?> GetByUserIdAndDateAsync(string userId, DateOnly date)
    {
        return await context.Set<HealthEntry>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == date);
    }

    public async Task UpsertAsync(HealthEntry entry)
    {
        var existing = await context.Set<HealthEntry>()
            .FirstOrDefaultAsync(x => x.UserId == entry.UserId && x.Date == entry.Date);

        if (existing is null)
        {
            await context.Set<HealthEntry>().AddAsync(entry);
        }
        else
        {
            existing.Weight = entry.Weight;
            existing.BloodPressure = entry.BloodPressure;
            existing.BloodSugar = entry.BloodSugar;
            existing.Water = entry.Water;
            existing.Temperature = entry.Temperature;
            existing.Monthlies = entry.Monthlies;
            existing.DictionaryStateJson = entry.DictionaryStateJson;
        }

        await context.SaveChangesAsync();
    }
}
