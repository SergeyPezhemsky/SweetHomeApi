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
    Task UpdateItemAsync(WardrobeItem item);

    /// <summary>Удаляет вещь, убирает её из образов и удаляет образы, где осталось меньше двух вещей.</summary>
    Task DeleteItemAsync(WardrobeItem item);

    Task<WardrobeItemImage?> GetImageAsync(string itemId);
    Task SetImageAsync(WardrobeItem item, byte[] data, string contentType);
    Task DeleteImageAsync(WardrobeItem item);

    Task<List<WardrobeOutfit>> GetOutfitsAsync(string userId);
    Task<WardrobeOutfit?> GetOutfitAsync(string userId, string outfitId);
    Task AddOutfitAsync(WardrobeOutfit outfit);
    Task DeleteOutfitAsync(WardrobeOutfit outfit);

    /// <summary>Атомарно увеличивает счётчик ношения вещей (без потерянных обновлений при параллельных запросах).</summary>
    Task MarkItemsWornAsync(string userId, IReadOnlyCollection<string> itemIds, DateTime wornAt);

    /// <summary>Атомарно увеличивает счётчик ношения образа.</summary>
    Task MarkOutfitWornAsync(string userId, string outfitId, DateTime wornAt);
}
