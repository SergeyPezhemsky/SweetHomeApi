using System.Net;
using System.Net.Http.Headers;
using Application.Modules.Wardrobe;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.Flatlay;

public class FlatlayClient(HttpClient httpClient, IOptions<FlatlayOptions> options) : IFlatlayClient
{
    public async Task<byte[]?> ProcessAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Flatlay:BaseUrl is not configured");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(file, "file", "photo");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));

        using var response = await httpClient.PostAsync($"{baseUrl}/process", content, timeout.Token);

        // 422 — сервис не нашёл на фото вещь: это не ошибка, просто оставляем оригинал.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(timeout.Token);
    }
}
