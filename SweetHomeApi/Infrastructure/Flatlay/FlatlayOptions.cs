namespace SweetHomeApi.Infrastructure.Flatlay;

public class FlatlayOptions
{
    /// <summary>Адрес сервиса flatlay (например, http://flatlay:8000). Пусто — удаление фона выключено.</summary>
    public string? BaseUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>Сколько раз повторить запрос, если сервис ещё не поднялся или недоступен.</summary>
    public int ConnectRetries { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 10;
}
