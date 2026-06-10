namespace Application.Modules.SmartHome;

public interface ISmartHomeService
{
    Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken);

    Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartHomeScenario>> GetScenariosAsync(string userId, CancellationToken cancellationToken);

    Task<SmartHomeScenario?> GetScenarioAsync(string scenarioId, string userId, CancellationToken cancellationToken);

    Task<SmartHomeScenario> CreateScenarioAsync(SmartHomeScenario scenario, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartHomeAutomation>> GetAutomationsAsync(string userId, CancellationToken cancellationToken);

    Task<SmartHomeAutomation?> GetAutomationAsync(string automationId, string userId, CancellationToken cancellationToken);

    Task<SmartHomeAutomation> CreateAutomationAsync(SmartHomeAutomation automation, string userId, CancellationToken cancellationToken);

    Task<SmartHomeAutomation?> UpdateAutomationAsync(SmartHomeAutomation automation, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartHomeEvent>> GetEventsAsync(string userId, int take, CancellationToken cancellationToken);

    Task<SmartHomeEvent> AddEventAsync(SmartHomeEvent smartHomeEvent, string userId, CancellationToken cancellationToken);
}
