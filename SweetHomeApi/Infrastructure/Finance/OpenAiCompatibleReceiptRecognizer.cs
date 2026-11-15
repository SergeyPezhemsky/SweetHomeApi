using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Modules.Finance;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Finance;

/// <summary>Распознаёт чек через POST {BaseUrl}/chat/completions с изображением в data URL.</summary>
public class OpenAiCompatibleReceiptRecognizer(HttpClient httpClient, IOptions<ReceiptRecognitionOptions> options) : IReceiptRecognizer
{
    public bool IsEnabled => options.Value.IsEnabled;

    public async Task<string> RecognizeAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsEnabled)
            throw new ReceiptRecognizerException("Receipt recognition is not configured");

        var payload = new
        {
            model = settings.ResolveModel(),
            temperature = 0,
            max_tokens = settings.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = ReceiptRecognitionPrompt.System },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = ReceiptRecognitionPrompt.User },
                        new { type = "image_url", image_url = new { url = $"data:{contentType};base64,{Convert.ToBase64String(image)}" } }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue(settings.AuthScheme.Trim(), settings.ApiKey!.Trim());
        if (!string.IsNullOrWhiteSpace(settings.Project))
            request.Headers.TryAddWithoutValidation("OpenAI-Project", settings.Project.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, settings.TimeoutSeconds)));

        string body;
        HttpStatusCode status;
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            status = response.StatusCode;
            body = await ReadLimitedAsync(response.Content, settings.MaxResponseBytes, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new ReceiptRecognizerUnavailableException("Recognition provider is unreachable", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Не повторяем: медленный ответ повторится таким же медленным, а каждая попытка стоит денег.
            // Пользователь может нажать «Повторить» в карточке чека.
            throw new ReceiptRecognizerException("Recognition provider timed out", ex);
        }

        if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500)
            throw new ReceiptRecognizerUnavailableException($"Recognition provider returned {(int)status}: {Shorten(body)}");

        if ((int)status is < 200 or >= 300)
            throw new ReceiptRecognizerException($"Recognition provider returned {(int)status}: {Shorten(body)}");

        return ExtractContent(body)
               ?? throw new ReceiptRecognizerException($"Recognition provider returned no message content: {Shorten(body)}");
    }

    /// <summary>Текст ответа: choices[0].message.content — строка или массив частей {type:"text", text}.</summary>
    public static string? ExtractContent(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0
                || !choices[0].TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content))
                return null;

            if (content.ValueKind == JsonValueKind.String)
                return content.GetString();

            if (content.ValueKind != JsonValueKind.Array)
                return null;

            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    builder.Append(text.GetString());
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Читает ответ, но не больше limit байт: неисправный провайдер не съест память процесса.</summary>
    private static async Task<string> ReadLimitedAsync(HttpContent content, int limit, CancellationToken cancellationToken)
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

    private static string Shorten(string value)
    {
        return value.Length > 500 ? value[..500] : value;
    }
}
