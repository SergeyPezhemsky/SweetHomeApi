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
        var widgets = await _mainWidgetRepository.GetByUserIdAsync(userId);

        // Пользователь без виджетов ещё не прошёл регистрацию полностью — ничего не добавляем.
        if (widgets.Count == 0)
            return widgets;

        var missing = new List<MainWidget>();
        var nextOrder = widgets.Max(w => w.Order) + 1;
        foreach (var alias in DefaultMainWidgets.BackfilledAliases)
        {
            if (widgets.Any(w => string.Equals(w.Alias, alias, StringComparison.OrdinalIgnoreCase)))
                continue;

            var widget = DefaultMainWidgets.CreateBackfilledWidget(alias, userId, nextOrder++);
            if (widget is not null)
                missing.Add(widget);
        }

        if (missing.Count == 0)
            return widgets;

        await _mainWidgetRepository.AddManyAsync(missing);
        return [..widgets, ..missing];
    }

    /// <summary>
    /// Редактирование списка виджетов.
    /// </summary>
    /// <param name="widgets">Список виджетов для обновления.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <returns>Задача обновления.</returns>
    public async Task UpdateWidgetsAsync(List<MainWidget> widgets, string userId)
    {
        // Дополнительная бизнес-логика перед обновлением, если требуется
        await _mainWidgetRepository.UpdateAsync(widgets, userId);
    }

    public async Task AddDefaultWidgetForUser(string userId)
    {
        var widgets = DefaultMainWidgets.GetDefaultWidgets(userId);

        await _mainWidgetRepository.AddManyAsync(widgets);
    }
}
