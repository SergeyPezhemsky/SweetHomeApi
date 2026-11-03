namespace Application.Modules.Wardrobe;

public interface IWardrobeService
{
    Task<WardrobeItemListResult> GetItemsAsync(string userId, WardrobeItemQuery query);
    Task<WardrobeItem?> GetItemAsync(string userId, string itemId);
    Task<WardrobeItem> CreateItemAsync(string userId, WardrobeItem item);
    Task<WardrobeItem?> UpdateItemAsync(string userId, string itemId, WardrobeItem item);
    Task<bool> DeleteItemAsync(string userId, string itemId);
    Task<WardrobeItem?> WearItemAsync(string userId, string itemId);

    Task<WardrobeItemImage?> GetImageAsync(string userId, string itemId);
    Task<WardrobeItem?> SetImageAsync(string userId, string itemId, byte[] data, string contentType);
    Task<WardrobeItem?> DeleteImageAsync(string userId, string itemId);

    WardrobeClassification Classify(string? name, string? notes, string? colorHex);
    Task<OutfitSuggestionResult> SuggestOutfitsAsync(string userId, OutfitSuggestionRequest request);

    Task<List<WardrobeOutfitDetails>> GetOutfitsAsync(string userId);
    Task<WardrobeOperationResult<WardrobeOutfitDetails>> CreateOutfitAsync(string userId, WardrobeOutfit outfit);
    Task<bool> DeleteOutfitAsync(string userId, string outfitId);
    Task<WardrobeOutfitDetails?> WearOutfitAsync(string userId, string outfitId);
}
