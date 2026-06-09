using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Modules.HomeAssistant;
using Microsoft.Extensions.Options;

namespace SweetHomeApi.Infrastructure.HomeAssistant;

public class HomeAssistantClient : IHomeAssistantClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly HomeAssistantOptions _options;

    public HomeAssistantClient(HttpClient httpClient, IOptions<HomeAssistantOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = CreateBaseUri(_options.BaseUrl);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    public async Task<IReadOnlyList<HomeAssistantEntityState>> GetStatesAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = CreateRequest(HttpMethod.Get, "api/states");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new HomeAssistantException("Home Assistant token is invalid or expired.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var states = await JsonSerializer.DeserializeAsync<List<HomeAssistantStateResponse>>(
            stream,
            JsonSerializerOptions,
            cancellationToken);

        return states?
            .Where(state => !string.IsNullOrWhiteSpace(state.EntityId))
            .Select(state => new HomeAssistantEntityState
            {
                EntityId = state.EntityId!,
                State = state.State ?? string.Empty,
                LastChanged = state.LastChanged,
                LastUpdated = state.LastUpdated,
                Attributes = state.Attributes ?? new Dictionary<string, JsonElement>()
            })
            .ToList() ?? [];
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new HomeAssistantConfigurationException("HomeAssistant:BaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new HomeAssistantConfigurationException("HomeAssistant:AccessToken is not configured.");
        }
    }

    private static Uri CreateBaseUri(string baseUrl)
    {
        var normalizedBaseUrl = baseUrl.Contains("://", StringComparison.Ordinal)
            ? baseUrl
            : $"http://{baseUrl}";

        if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new HomeAssistantConfigurationException(
                "HomeAssistant:BaseUrl must be an HTTP URL, for example http://192.168.1.143:8123.");
        }

        return uri;
    }

    private sealed class HomeAssistantStateResponse
    {
        [JsonPropertyName("entity_id")]
        public string? EntityId { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("last_changed")]
        public DateTimeOffset LastChanged { get; set; }

        [JsonPropertyName("last_updated")]
        public DateTimeOffset LastUpdated { get; set; }

        [JsonPropertyName("attributes")]
        public Dictionary<string, JsonElement>? Attributes { get; set; }
    }
}
