using Application.Modules.Widgets.Seeds;

namespace Application.Modules.Widgets;

public class WidgetsService : IWidgetsService
{
    private readonly IMainWidgetRepository _mainWidgetRepository;

    public WidgetsService(IMainWidgetRepository mainWidgetRepository)
    {
        _mainWidgetRepository = mainWidgetRepository;
    }

    /// <summary>
    /// Получение виджетов пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <returns>Список виджетов.</returns>
    public async Task<List<MainWidget>> GetUserWidgetsAsync(string userId)
    {
        return await _mainWidgetRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Редактирование списка виджетов.
    /// </summary>
    /// <param name="widgets">Список виджетов для обновления.</param>
    /// <returns>Задача обновления.</returns>
    public async Task UpdateWidgetsAsync(List<MainWidget> widgets)
    {
        // Дополнительная бизнес-логика перед обновлением, если требуется
        await _mainWidgetRepository.UpdateAsync(widgets);
    }

    public async Task AddDefaultWidgetForUser(string userId)
    {
        var widgets = DefaultMainWidgets.GetDefaultWidgets(userId);

        await _mainWidgetRepository.AddManyAsync(widgets);
    }
}
