namespace Application.Modules.HomeAssistant;

public interface IHomeAssistantService
{
    Task<IReadOnlyList<HomeAssistantCatalogWidget>> GetWidgetCatalogAsync(CancellationToken cancellationToken);
}
