namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>
/// OpenAI-совместимый vision-провайдер для распознавания чеков (по умолчанию — Yandex AI Studio).
/// Пустой ApiKey или Model — распознавание выключено, фото сохраняются для ручного ввода.
/// </summary>
public class ReceiptRecognitionOptions
{
    public string BaseUrl { get; set; } = "https://ai.api.cloud.yandex.net/v1";

    public string? ApiKey { get; set; }

    /// <summary>Схема заголовка Authorization: «Api-Key» для Yandex AI Studio, «Bearer» для остальных.</summary>
    public string AuthScheme { get; set; } = "Api-Key";

    /// <summary>Идентификатор каталога Yandex Cloud: заголовок OpenAI-Project и префикс модели gpt://{Project}/.</summary>
    public string? Project { get; set; }

    /// <summary>Модель с поддержкой изображений. Без схемы и с заданным Project превращается в gpt://{Project}/{Model}.</summary>
    public string? Model { get; set; } = "qwen3.6-35b-a3b";

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>С запасом на длинный чек (150 позиций по ~50 токенов); оборванный ответ всё равно достраивается.</summary>
    public int MaxTokens { get; set; } = 12000;

    /// <summary>Предел размера ответа провайдера.</summary>
    public int MaxResponseBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>Сколько раз повторить запрос, если провайдер недоступен (сеть, 429, 5xx).</summary>
    public int Retries { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 15;

    /// <summary>Через сколько повторить чек, если не удалось записать результат в базу.</summary>
    public int InternalRetryDelaySeconds { get; set; } = 60;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey)
                             && !string.IsNullOrWhiteSpace(Model)
                             && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)
                             && !MissingYandexFolder;

    /// <summary>Yandex AI Studio без идентификатора каталога отклоняет каждый запрос — это ошибка настройки, а не сбой.</summary>
    public bool MissingYandexFolder => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
                                       && uri.Host.EndsWith("yandex.net", StringComparison.OrdinalIgnoreCase)
                                       && string.IsNullOrWhiteSpace(Project)
                                       && !(Model ?? string.Empty).Contains("://", StringComparison.Ordinal);

    public string ResolveModel()
    {
        var model = Model!.Trim();
        return model.Contains("://", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(Project)
            ? model
            : $"gpt://{Project.Trim()}/{model}";
    }
}
