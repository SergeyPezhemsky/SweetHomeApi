namespace SweetHomeApi.Infrastructure.HomeAssistant;

public class HomeAssistantOptions
{
    public string? BaseUrl { get; set; }

    public string? AccessToken { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 10;
}
