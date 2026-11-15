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

    /// <summary>Haiku — дешёвый и достаточный для чеков; claude-sonnet-5-5 — точнее и примерно в 10–20 раз дороже.</summary>
    public string Model { get; set; } = "claude-haiku-5-5";

    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Прокси только для запросов к Anthropic: http://host:port, http://user:pass@host:port или socks5://host:port.
    /// Пусто — напрямую.
    /// </summary>
    public string? ProxyUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 240;

    public int MaxTokens { get; set; } = 12000;

    /// <summary>Выключить adaptive thinking (по умолчанию у Haiku 5.5 включён и тратит оплачиваемые токены из max_tokens).</summary>
    public bool DisableThinking { get; set; } = true;

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
