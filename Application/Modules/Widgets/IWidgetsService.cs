namespace Application.Modules.Widgets;

public interface IWidgetsService
{
    /// <summary>
    /// Получение виджетов пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <returns>Список виджетов.</returns>
    Task<List<MainWidget>> GetUserWidgetsAsync(string userId);

    /// <summary>
    /// Редактирование списка виджетов.
    /// </summary>
    /// <param name="widgets">Список виджетов для обновления.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <returns>Задача обновления.</returns>
    Task UpdateWidgetsAsync(List<MainWidget> widgets, string userId);
    
    Task AddDefaultWidgetForUser(string userId);
}
