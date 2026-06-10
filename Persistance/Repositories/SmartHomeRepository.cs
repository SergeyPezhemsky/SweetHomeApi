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

    public async Task<IReadOnlyList<SmartHomeScenario>> GetScenariosAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await context.Set<SmartHomeScenario>()
            .Where(scenario => scenario.UserId == userId)
            .OrderBy(scenario => scenario.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<SmartHomeScenario?> GetScenarioAsync(
        string scenarioId,
        string userId,
        CancellationToken cancellationToken)
    {
        return context.Set<SmartHomeScenario>()
            .FirstOrDefaultAsync(
                scenario => scenario.Id == scenarioId && scenario.UserId == userId,
                cancellationToken);
    }

    public async Task AddScenarioAsync(SmartHomeScenario scenario, CancellationToken cancellationToken)
    {
        await context.Set<SmartHomeScenario>().AddAsync(scenario, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SmartHomeAutomation>> GetAutomationsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await context.Set<SmartHomeAutomation>()
            .Where(automation => automation.UserId == userId)
            .OrderBy(automation => automation.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<SmartHomeAutomation?> GetAutomationAsync(
        string automationId,
        string userId,
        CancellationToken cancellationToken)
    {
        return context.Set<SmartHomeAutomation>()
            .FirstOrDefaultAsync(
                automation => automation.Id == automationId && automation.UserId == userId,
                cancellationToken);
    }

    public async Task AddAutomationAsync(SmartHomeAutomation automation, CancellationToken cancellationToken)
    {
        await context.Set<SmartHomeAutomation>().AddAsync(automation, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAutomationAsync(SmartHomeAutomation automation, CancellationToken cancellationToken)
    {
        context.Set<SmartHomeAutomation>().Update(automation);
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SmartHomeEvent>> GetEventsAsync(
        string userId,
        int take,
        CancellationToken cancellationToken)
    {
        return await context.Set<SmartHomeEvent>()
            .Where(smartHomeEvent => smartHomeEvent.UserId == userId)
            .OrderByDescending(smartHomeEvent => smartHomeEvent.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddEventAsync(SmartHomeEvent smartHomeEvent, CancellationToken cancellationToken)
    {
        await context.Set<SmartHomeEvent>().AddAsync(smartHomeEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
