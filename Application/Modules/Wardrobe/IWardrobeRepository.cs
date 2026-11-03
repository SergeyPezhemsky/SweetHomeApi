namespace Application.Modules.Wardrobe;

public interface IWardrobeRepository
{
    Task<List<WardrobeItem>> GetItemsAsync(string userId, WardrobeItemQuery query);
    Task<Dictionary<WardrobeCategory, int>> GetCategoryCountsAsync(string userId);
    Task<List<WardrobeItem>> GetAllItemsAsync(string userId);
    Task<List<WardrobeItem>> GetItemsByIdsAsync(string userId, IReadOnlyCollection<string> itemIds);
    Task<WardrobeItem?> GetItemAsync(string userId, string itemId);
    Task<WardrobeItem?> GetItemAsNoTrackingAsync(string userId, string itemId);
    Task<List<WardrobeItem>> GetItemsByIdsAsNoTrackingAsync(string userId, IReadOnlyCollection<string> itemIds);
    Task<WardrobeOutfit?> GetOutfitAsNoTrackingAsync(string userId, string outfitId);
    Task AddItemAsync(WardrobeItem item);
    /// <summary>Сохраняет изменения вещи. Возвращает false, если вещь успели удалить.</summary>
    Task<bool> UpdateItemAsync(WardrobeItem item);

    /// <summary>Удаляет вещь, убирает её из образов и удаляет образы, где осталось меньше двух вещей.</summary>
    Task DeleteItemAsync(WardrobeItem item);

    /// <summary>Байты нужной версии фото (грузится только один столбец).</summary>
    Task<WardrobeImageContent?> GetImageAsync(string itemId, WardrobeImageVariant variant);

    /// <summary>
    /// Сохраняет новый оригинал, сбрасывает обработанную версию и увеличивает ревизию.
    /// Возвращает false, если вещь успели удалить.
    /// </summary>
    Task<bool> SetImageAsync(WardrobeItem item, byte[] data, string contentType, bool queueProcessing);

    Task DeleteImageAsync(WardrobeItem item);

    /// <summary>Выбирает показываемую версию. False — если обработанной версии нет.</summary>
    Task<bool> SetImageVariantAsync(WardrobeItem item, WardrobeImageVariant variant);

    /// <summary>Ставит фото в очередь повторно. False — если фото нет.</summary>
    Task<bool> MarkImagePendingAsync(WardrobeItem item);

    Task<List<string>> GetPendingImageItemIdsAsync();
    Task<WardrobePendingImage?> GetPendingImageAsync(string itemId);

    /// <summary>Сохраняет результат обработки, если ревизия фото не изменилась. Возвращает false, если результат устарел.</summary>
    Task<bool> SaveProcessedImageAsync(string itemId, int revision, byte[] data, string contentType);

    Task MarkImageProcessingFailedAsync(string itemId, int revision);

    Task<List<WardrobeOutfit>> GetOutfitsAsync(string userId);
    Task<WardrobeOutfit?> GetOutfitAsync(string userId, string outfitId);
    /// <summary>Добавляет образ, если все его вещи всё ещё существуют. Возвращает false, если какой-то вещи уже нет.</summary>
    Task<bool> AddOutfitAsync(WardrobeOutfit outfit);
    Task DeleteOutfitAsync(WardrobeOutfit outfit);

    /// <summary>
    /// Обновляет образ, если все его вещи существуют (проверка под блокировкой образов пользователя).
    /// Возвращает статус: Ok, NotFound (образа нет) или InvalidItems.
    /// </summary>
    Task<WardrobeOperationStatus> UpdateOutfitAsync(WardrobeOutfit outfit);

    /// <summary>Атомарно увеличивает счётчик ношения вещей (без потерянных обновлений при параллельных запросах).</summary>
    Task MarkItemsWornAsync(string userId, IReadOnlyCollection<string> itemIds, DateTime wornAt);

    /// <summary>Атомарно увеличивает счётчик ношения образа.</summary>
    Task MarkOutfitWornAsync(string userId, string outfitId, DateTime wornAt);
}
