namespace Application.Modules.SmartHome;

public class SmartHomeService(ISmartHomeRepository smartHomeRepository) : ISmartHomeService
{
    public Task<SmartHomeLayout> GetLayoutAsync(string userId, CancellationToken cancellationToken)
    {
        return smartHomeRepository.GetLayoutAsync(userId, cancellationToken);
    }

    public Task ReplaceLayoutAsync(SmartHomeLayout layout, string userId, CancellationToken cancellationToken)
    {
        var roomIds = layout.Rooms
            .Select(room => room.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var room in layout.Rooms)
        {
            room.UserId = userId;
        }

        foreach (var widget in layout.Widgets)
        {
            widget.UserId = userId;

            if (widget.RoomId is not null && !roomIds.Contains(widget.RoomId))
            {
                widget.RoomId = null;
            }
        }

        return smartHomeRepository.ReplaceLayoutAsync(layout, userId, cancellationToken);
    }
}
