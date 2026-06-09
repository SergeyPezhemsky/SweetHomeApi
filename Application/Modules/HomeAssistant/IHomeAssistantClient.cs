namespace Application.Modules.HomeAssistant;

public interface IHomeAssistantClient
{
    Task<IReadOnlyList<HomeAssistantEntityState>> GetStatesAsync(CancellationToken cancellationToken);
}
