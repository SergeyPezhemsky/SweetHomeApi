namespace Application.Modules.Widgets;

public interface IMainWidgetRepository
{
    Task<List<MainWidget>> GetByUserIdAsync(string userId);
    Task UpdateAsync(List<MainWidget> mainWidget, string userId);
    Task AddManyAsync(List<MainWidget> mainWidget);

    /// <summary>
    /// Добавляет пользователю виджеты с алиасами, которых у него ещё нет.
    /// Безопасно при параллельных запросах: возвращает только реально добавленные виджеты.
    /// </summary>
    Task<List<MainWidget>> AddMissingAsync(string userId, IReadOnlyList<MainWidget> candidates);
}
