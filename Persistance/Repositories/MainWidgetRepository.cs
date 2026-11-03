using Application.Modules.Widgets;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class MainWidgetRepository(SweetHomeDbContext context) : IMainWidgetRepository
{
    public async Task<List<MainWidget>> GetByUserIdAsync(string userId)
    {
        return await context.Set<MainWidget>()
            .Where(w => w.UserId == userId)
            .ToListAsync();
    }

    public async Task UpdateAsync(List<MainWidget> mainWidgets, string userId)
    {
        foreach (var widget in mainWidgets)
        {
            var existingWidget = await context.Set<MainWidget>()
                .FirstOrDefaultAsync(w => w.Id == widget.Id && w.UserId == userId);

            if (existingWidget != null)
            {
                existingWidget.Alias = widget.Alias;
                existingWidget.Order = widget.Order;
                existingWidget.Name = widget.Name;
                existingWidget.Icon = widget.Icon;
                existingWidget.Size = widget.Size;
                existingWidget.Hide = widget.Hide;
            }
        }

        await context.SaveChangesAsync();
    }
    
    public async Task<List<MainWidget>> AddMissingAsync(string userId, IReadOnlyList<MainWidget> candidates)
    {
        if (candidates.Count == 0)
            return [];

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Транзакционная advisory-блокировка на пользователя: параллельные запросы проверяют и добавляют по очереди.
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"widgets:" + userId}))");

        var existingAliases = await context.Set<MainWidget>()
            .Where(w => w.UserId == userId)
            .Select(w => w.Alias)
            .ToListAsync();

        var added = candidates
            .Where(c => !existingAliases.Contains(c.Alias, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (added.Count > 0)
        {
            await context.Set<MainWidget>().AddRangeAsync(added);
            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return added;
    }

    public async Task AddManyAsync(List<MainWidget> mainWidgets)
    {
        await context.Set<MainWidget>().AddRangeAsync(mainWidgets);
        await context.SaveChangesAsync();
    }
}
