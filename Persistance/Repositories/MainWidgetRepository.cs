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
    
    public async Task AddManyAsync(List<MainWidget> mainWidgets)
    {
        await context.Set<MainWidget>().AddRangeAsync(mainWidgets);
        await context.SaveChangesAsync();
    }
}
