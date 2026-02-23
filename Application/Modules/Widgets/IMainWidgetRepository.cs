namespace Application.Modules.Widgets;

public interface IMainWidgetRepository
{
    Task<List<MainWidget>> GetByUserIdAsync(string userId);
    Task UpdateAsync(List<MainWidget> mainWidget, string userId);
    Task AddManyAsync(List<MainWidget> mainWidget);
}
