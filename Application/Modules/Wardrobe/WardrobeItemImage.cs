namespace Application.Modules.Wardrobe;

/// <summary>
/// Фото вещи хранится отдельно, чтобы списки вещей не тянули байты изображений.
/// </summary>
public class WardrobeItemImage
{
    public string ItemId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public WardrobeItem Item { get; set; } = null!;
}
