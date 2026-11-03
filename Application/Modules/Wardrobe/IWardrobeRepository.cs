namespace Application.Modules.Wardrobe;

public interface IWardrobeRepository
{
    Task<List<WardrobeItem>> GetItemsAsync(string userId, WardrobeItemQuery query);
    Task<Dictionary<WardrobeCategory, int>> GetCategoryCountsAsync(string userId);
    Task<List<WardrobeItem>> GetAllItemsAsync(string userId);
    Task<List<WardrobeItem>> GetItemsByIdsAsync(string userId, IReadOnlyCollection<string> itemIds);
    Task<WardrobeItem?> GetItemAsync(string userId, string itemId);
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

    /// <summary>Сохраняет изменения отслеживаемых сущностей (отметка «надето»).</summary>
    Task SaveChangesAsync();
}
