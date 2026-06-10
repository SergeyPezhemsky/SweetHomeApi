using Application.Modules.SmartHome;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class SmartHomeRepository(SweetHomeDbContext context) : ISmartHomeRepository
{
    public async Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken)
    {
        var rooms = await context.Set<SmartHomeRoom>()
            .Where(room => room.UserId == userId)
            .OrderBy(room => room.Order)
            .ThenBy(room => room.Name)
            .ToListAsync(cancellationToken);

        var widgets = await context.Set<SmartHomeWidget>()
            .Where(widget => widget.UserId == userId)
            .OrderBy(widget => widget.Order)
            .ThenBy(widget => widget.Name)
            .ToListAsync(cancellationToken);

        return new SmartHomeLayout
        {
            Rooms = rooms,
            Widgets = widgets
        };
    }

    public async Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken)
    {
        await context.Set<SmartHomeWidget>()
            .Where(widget => widget.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Set<SmartHomeRoom>()
            .Where(room => room.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (layout.Rooms.Count > 0)
        {
            await context.Set<SmartHomeRoom>().AddRangeAsync(layout.Rooms, cancellationToken);
        }

        if (layout.Widgets.Count > 0)
        {
            await context.Set<SmartHomeWidget>().AddRangeAsync(layout.Widgets, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
