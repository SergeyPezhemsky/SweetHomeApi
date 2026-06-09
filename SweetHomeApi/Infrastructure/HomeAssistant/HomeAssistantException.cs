namespace SweetHomeApi.Infrastructure.HomeAssistant;

public class HomeAssistantException(string message) : Exception(message);

public class HomeAssistantConfigurationException(string message) : HomeAssistantException(message);
