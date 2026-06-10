namespace Application.Modules.SmartHome;

public interface ISmartHomeRepository
{
    Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken);

    Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken);
}
