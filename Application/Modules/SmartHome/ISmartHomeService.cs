namespace Application.Modules.SmartHome;

public interface ISmartHomeService
{
    Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken);

    Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken);
}
