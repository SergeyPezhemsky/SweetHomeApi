namespace Application.Modules.HomeAssistant;

public interface IHomeAssistantService
{
    Task<IReadOnlyList<HomeAssistantCatalogWidget>> GetWidgetCatalogAsync(CancellationToken cancellationToken);

    Task ExecuteActionAsync(HomeAssistantActionRequest request, CancellationToken cancellationToken);
}
