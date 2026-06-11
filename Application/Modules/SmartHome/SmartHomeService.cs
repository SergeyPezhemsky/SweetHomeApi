namespace Application.Modules.SmartHome;

public class SmartHomeService(ISmartHomeRepository smartHomeRepository) : ISmartHomeService
{
    public Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetLayoutAsync(userId, cancellationToken);
    }

    public Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken)
    {
        var roomIds = layout.Rooms
            .Select(room => room.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var room in layout.Rooms)
        {
            room.UserId = userId;
        }

        foreach (var widget in layout.Widgets)
        {
            widget.UserId = userId;

            if (widget.RoomId is not null && !roomIds.Contains(widget.RoomId))
            {
                widget.RoomId = null;
            }
        }

        return smartHomeRepository.ReplaceLayoutAsync(layout, userId, cancellationToken);
    }

    public Task<IReadOnlyList<SmartHomeScenario>> GetScenariosAsync(string userId, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetScenariosAsync(userId, cancellationToken);
    }

    public Task<SmartHomeScenario?> GetScenarioAsync(string scenarioId, string userId, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetScenarioAsync(scenarioId, userId, cancellationToken);
    }

    public async Task<SmartHomeScenario> CreateScenarioAsync(
        SmartHomeScenario scenario,
        string userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        scenario.Id = string.IsNullOrWhiteSpace(scenario.Id) ? Guid.NewGuid().ToString() : scenario.Id;
        scenario.UserId = userId;
        scenario.CreatedAt = now;
        scenario.UpdatedAt = now;

        await smartHomeRepository.AddScenarioAsync(scenario, cancellationToken);
        return scenario;
    }

    public Task<IReadOnlyList<SmartHomeAutomation>> GetAutomationsAsync(string userId, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetAutomationsAsync(userId, cancellationToken);
    }

    public Task<SmartHomeAutomation?> GetAutomationAsync(string automationId, string userId, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetAutomationAsync(automationId, userId, cancellationToken);
    }

    public async Task<SmartHomeAutomation> CreateAutomationAsync(
        SmartHomeAutomation automation,
        string userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        automation.Id = string.IsNullOrWhiteSpace(automation.Id) ? Guid.NewGuid().ToString() : automation.Id;
        automation.UserId = userId;
        automation.CreatedAt = now;
        automation.UpdatedAt = now;

        await smartHomeRepository.AddAutomationAsync(automation, cancellationToken);
        return automation;
    }

    public async Task<SmartHomeAutomation?> UpdateAutomationAsync(
        SmartHomeAutomation automation,
        string userId,
        CancellationToken cancellationToken)
    {
        var existing = await smartHomeRepository.GetAutomationAsync(automation.Id, userId, cancellationToken);
        if (existing is null)
            return null;

        existing.Name = automation.Name;
        existing.Enabled = automation.Enabled;
        existing.TriggerJson = automation.TriggerJson;
        existing.ConditionsJson = automation.ConditionsJson;
        existing.ActionsJson = automation.ActionsJson;
        existing.UpdatedAt = DateTime.UtcNow;

        await smartHomeRepository.UpdateAutomationAsync(existing, cancellationToken);
        return existing;
    }

    public Task<IReadOnlyList<SmartHomeEvent>> GetEventsAsync(string userId, int take, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetEventsAsync(userId, Math.Clamp(take, 1, 200), cancellationToken);
    }

    public async Task<SmartHomeEvent> AddEventAsync(
        SmartHomeEvent smartHomeEvent,
        string userId,
        CancellationToken cancellationToken)
    {
        smartHomeEvent.Id = string.IsNullOrWhiteSpace(smartHomeEvent.Id) ? Guid.NewGuid().ToString() : smartHomeEvent.Id;
        smartHomeEvent.UserId = userId;
        smartHomeEvent.CreatedAt = smartHomeEvent.CreatedAt == default ? DateTime.UtcNow : smartHomeEvent.CreatedAt;
        smartHomeEvent.PayloadJson = string.IsNullOrWhiteSpace(smartHomeEvent.PayloadJson) ? "{}" : smartHomeEvent.PayloadJson;

        await smartHomeRepository.AddEventAsync(smartHomeEvent, cancellationToken);
        return smartHomeEvent;
    }
}
