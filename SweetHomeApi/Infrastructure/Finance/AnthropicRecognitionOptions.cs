using System.Net;

namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>
/// Anthropic Messages API как провайдер распознавания чеков (секция Finance:Recognition:Anthropic).
/// Пустой ApiKey — провайдер выключен. Anthropic не обслуживает запросы из России:
/// с российского сервера нужен ProxyUrl за её пределами.
/// </summary>
public class AnthropicRecognitionOptions
{
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    public string? ApiKey { get; set; }

    /// <summary>Haiku — дешёвый и достаточный для чеков; claude-sonnet-5-5 точнее и дороже (оценка по прайсу — в 10–20 раз).</summary>
    public string Model { get; set; } = "claude-haiku-5-5";

    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Прокси только для запросов к Anthropic: http://host:port, http://user:pass@host:port или socks5://host:port.
    /// Пусто — напрямую.
    /// </summary>
    public string? ProxyUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 240;

    public int MaxTokens { get; set; } = 12000;

    /// <summary>
    /// Режим рассуждений: auto (по модели), disabled, between_tools, adaptive. Модели 5.5 различаются:
    /// Haiku принимает disabled, Sonnet — только between_tools (disabled → 400), у Opus рассуждения всегда включены
    /// (disabled → 400, поле не передаём). auto выбирает самый «тихий» допустимый режим: для извлечения полей рассуждения не нужны,
    /// а их оплачиваемые токены идут из max_tokens.
    /// </summary>
    public string Thinking { get; set; } = "auto";

    /// <summary>Глубина рассуждений (output_config.effort): low / medium / high. Пусто — по умолчанию модели.</summary>
    public string? Effort { get; set; }

    /// <summary>Значение поля thinking для запроса; null — поле не передаём.</summary>
    public string? ResolveThinking()
    {
        var mode = (Thinking ?? "auto").Trim().ToLowerInvariant();
        if (mode is "disabled" or "between_tools" or "adaptive")
            return mode;
        if (mode is "omit" or "none" or "")
            return null;

        var model = (Model ?? string.Empty).ToLowerInvariant();
        if (model.Contains("haiku", StringComparison.Ordinal))
            return "disabled";
        if (model.Contains("sonnet", StringComparison.Ordinal))
            return "between_tools";
        return null;
    }

    public int MaxResponseBytes { get; set; } = 2 * 1024 * 1024;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey)
                             && !string.IsNullOrWhiteSpace(Model)
                             && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)
                             && (string.IsNullOrWhiteSpace(ProxyUrl) || TryCreateProxy(ProxyUrl, out _));

    /// <summary>Прокси из URL; логин и пароль из userinfo уходят в учётные данные прокси.</summary>
    public static bool TryCreateProxy(string? url, out IWebProxy? proxy)
    {
        proxy = null;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "socks4" or "socks4a" or "socks5"))
            return false;

        var address = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;
        var webProxy = new WebProxy(address);
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            webProxy.Credentials = new NetworkCredential(Uri.UnescapeDataString(parts[0]),
                parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
        }

        proxy = webProxy;
        return true;
    }
}
