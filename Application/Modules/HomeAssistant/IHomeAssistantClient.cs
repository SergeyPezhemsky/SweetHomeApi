namespace Application.Modules.HomeAssistant;

public interface IHomeAssistantClient
{
    Task<IReadOnlyList<HomeAssistantEntityState>> GetStatesAsync(CancellationToken cancellationToken);

    Task CallServiceAsync(
        string domain,
        string service,
        IReadOnlyDictionary<string, object?> data,
        CancellationToken cancellationToken);
}
