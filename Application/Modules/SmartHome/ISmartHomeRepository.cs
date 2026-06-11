namespace Application.Modules.SmartHome;

public interface ISmartHomeRepository
{
    Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken);

    Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartHomeScenario>> GetScenariosAsync(string userId, CancellationToken cancellationToken);

    Task<SmartHomeScenario?> GetScenarioAsync(string scenarioId, string userId, CancellationToken cancellationToken);

    Task AddScenarioAsync(SmartHomeScenario scenario, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartHomeAutomation>> GetAutomationsAsync(string userId, CancellationToken cancellationToken);

    Task<SmartHomeAutomation?> GetAutomationAsync(string automationId, string userId, CancellationToken cancellationToken);

    Task AddAutomationAsync(SmartHomeAutomation automation, CancellationToken cancellationToken);

    Task UpdateAutomationAsync(SmartHomeAutomation automation, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartHomeEvent>> GetEventsAsync(string userId, int take, CancellationToken cancellationToken);

    Task AddEventAsync(SmartHomeEvent smartHomeEvent, CancellationToken cancellationToken);
}
