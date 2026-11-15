using System.Net;
using System.Text;
using System.Text.Json;
using Application.Modules.Finance;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>Распознаёт чек через Anthropic Messages API: POST {BaseUrl}/v1/messages с изображением в base64.</summary>
public class AnthropicReceiptRecognizer(HttpClient httpClient, IOptions<AnthropicRecognitionOptions> options) : IReceiptRecognizer
{
    public const string ProviderName = "anthropic";

    public string Provider => ProviderName;

    public string Model => options.Value.Model.Trim();

    public bool IsEnabled => options.Value.IsEnabled;

    public async Task<string> RecognizeAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsEnabled)
            throw new ReceiptRecognizerException("Anthropic recognition is not configured");

        // temperature не передаём: Claude Haiku 5.5 возвращает ошибку на temperature/top_p/top_k (breaking change).
        // Рассуждения для извлечения полей не нужны — выбираем самый «тихий» режим, который принимает модель.
        var payload = new Dictionary<string, object>
        {
            ["model"] = Model,
            ["max_tokens"] = settings.MaxTokens,
            ["system"] = ReceiptRecognitionPrompt.System,
            ["messages"] = BuildMessages(image, contentType)
        };
        if (settings.ResolveThinking() is { } thinking)
            payload["thinking"] = new { type = thinking };
        if (settings.ResolveEffort() is { } effort)
            payload["output_config"] = new { effort };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/v1/messages");
        request.Headers.TryAddWithoutValidation("x-api-key", settings.ApiKey!.Trim());
        request.Headers.TryAddWithoutValidation("anthropic-version", settings.ApiVersion);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, settings.TimeoutSeconds)));

        string body;
        HttpStatusCode status;
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            status = response.StatusCode;
            body = await ProviderHttp.ReadLimitedAsync(response.Content, settings.MaxResponseBytes, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new ReceiptRecognizerUnavailableException("Anthropic is unreachable", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Как и у OpenAI-совместимого провайдера: медленный ответ не повторяем автоматически.
            throw new ReceiptRecognizerException("Anthropic timed out", ex);
        }

        // 529 — «overloaded» у Anthropic: временно, как 503.
        if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500)
            throw new ReceiptRecognizerUnavailableException($"Anthropic returned {(int)status}: {ProviderHttp.Shorten(body)}");

        // 403 с российского IP (страна не поддерживается) — ошибка настройки, а не сбой: повтор не поможет.
        if ((int)status is < 200 or >= 300)
            throw new ReceiptRecognizerException($"Anthropic returned {(int)status}: {ProviderHttp.Shorten(body)}");

        return ExtractText(body)
               ?? throw new ReceiptRecognizerException($"Anthropic returned no text content: {ProviderHttp.Shorten(body)}");
    }

    private static object[] BuildMessages(byte[] image, string contentType)
    {
        return
        [
            new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "image",
                        source = new { type = "base64", media_type = contentType, data = Convert.ToBase64String(image) }
                    },
                    new { type = "text", text = ReceiptRecognitionPrompt.User }
                }
            }
        ];
    }

    /// <summary>Текст ответа: все блоки content с type = "text" подряд (блоки thinking пропускаются).</summary>
    public static string? ExtractText(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                return null;

            var builder = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object
                    && block.TryGetProperty("type", out var type) && type.GetString() == "text"
                    && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    builder.Append(text.GetString());
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>Общие для провайдеров мелочи HTTP.</summary>
public static class ProviderHttp
{
    /// <summary>Читает ответ, но не больше limit байт: неисправный провайдер не съест память процесса.</summary>
    public static async Task<string> ReadLimitedAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > limit)
                throw new ReceiptRecognizerException($"Recognition provider response exceeds {limit} bytes");
            memory.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    public static string Shorten(string value)
    {
        return value.Length > 500 ? value[..500] : value;
    }
}

/// <summary>Порядок провайдеров из настройки Finance:Recognition:Order («openai,anthropic»): первый — основной, остальные — запасные.</summary>
public static class RecognizerOrder
{
    public const string Default = "openai,anthropic";

    /// <summary>Включённые провайдеры в заданном порядке. Не названный в порядке провайдер не используется.</summary>
    public static List<IReceiptRecognizer> Arrange(IEnumerable<IReceiptRecognizer> recognizers, string? order)
    {
        var names = Names(order);
        return recognizers
            .Where(x => x.IsEnabled && names.Contains(x.Provider))
            .GroupBy(x => x.Provider)
            .Select(g => g.First())
            .OrderBy(x => names.IndexOf(x.Provider))
            .ToList();
    }

    public static bool Includes(string? order, string provider) => Names(order).Contains(provider);

    private static List<string> Names(string? order)
    {
        return (string.IsNullOrWhiteSpace(order) ? Default : order)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}
