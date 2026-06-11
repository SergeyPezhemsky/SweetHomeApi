namespace Application.Modules.HomeAssistant;

public interface IHomeAssistantService
{
    Task<IReadOnlyList<HomeAssistantCatalogWidget>> GetWidgetCatalogAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetUnknownWidgetEntityIdsAsync(
        IReadOnlyCollection<string> entityIds,
        CancellationToken cancellationToken);

    Task ExecuteActionAsync(HomeAssistantActionRequest request, CancellationToken cancellationToken);
}
