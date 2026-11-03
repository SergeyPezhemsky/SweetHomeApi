namespace Application.Modules.Wardrobe;

/// <summary>
/// Фото вещи хранится отдельно, чтобы списки вещей не тянули байты изображений.
/// Data — оригинал, ProcessedData — версия без фона (если обработка прошла).
/// </summary>
public class WardrobeItemImage
{
    public string ItemId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public byte[]? ProcessedData { get; set; }
    public string? ProcessedContentType { get; set; }
    public WardrobeImageStatus ProcessingStatus { get; set; } = WardrobeImageStatus.NONE;

    /// <summary>Номер загрузки: результат обработки сохраняется, только если фото с тех пор не заменили.</summary>
    public int Revision { get; set; }

    public DateTime UpdatedAt { get; set; }
    public WardrobeItem Item { get; set; } = null!;
}

/// <summary>Байты одной версии фото для отдачи клиенту.</summary>
public record WardrobeImageContent(byte[] Data, string ContentType);

/// <summary>Фото, ожидающее обработки.</summary>
public record WardrobePendingImage(string ItemId, byte[] Data, string ContentType, int Revision);
