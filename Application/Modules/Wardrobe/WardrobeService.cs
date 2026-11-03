namespace Application.Modules.Wardrobe;

public class WardrobeService(IWardrobeRepository repository, TimeProvider? timeProvider = null) : IWardrobeService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public async Task<WardrobeItemListResult> GetItemsAsync(string userId, WardrobeItemQuery query)
    {
        var items = await repository.GetItemsAsync(userId, query);
        var counts = await repository.GetCategoryCountsAsync(userId);

        foreach (var category in Enum.GetValues<WardrobeCategory>())
            counts.TryAdd(category, 0);

        return new WardrobeItemListResult
        {
            Items = items,
            Total = items.Count,
            Counts = counts
        };
    }

    public Task<WardrobeItem?> GetItemAsync(string userId, string itemId)
    {
        return repository.GetItemAsync(userId, itemId);
    }

    public async Task<WardrobeItem> CreateItemAsync(string userId, WardrobeItem item)
    {
        var now = UtcNow;
        item.Id = Guid.NewGuid().ToString();
        item.UserId = userId;
        item.CreatedAt = now;
        item.UpdatedAt = now;
        item.HasImage = false;
        item.ImageUpdatedAt = null;
        item.WearCount = 0;
        item.LastWornAt = null;
        Normalize(item);

        await repository.AddItemAsync(item);
        return item;
    }

    public async Task<WardrobeItem?> UpdateItemAsync(string userId, string itemId, WardrobeItem item)
    {
        var existing = await repository.GetItemAsync(userId, itemId);
        if (existing is null)
            return null;

        existing.Name = item.Name;
        existing.Category = item.Category;
        existing.Colors = item.Colors;
        existing.Seasons = item.Seasons;
        existing.Styles = item.Styles;
        existing.Warmth = item.Warmth;
        existing.Brand = item.Brand;
        existing.Notes = item.Notes;
        existing.IsFavorite = item.IsFavorite;
        existing.UpdatedAt = UtcNow;
        Normalize(existing);

        await repository.UpdateItemAsync(existing);
        return existing;
    }

    public async Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        var existing = await repository.GetItemAsync(userId, itemId);
        if (existing is null)
            return false;

        await repository.DeleteItemAsync(existing);
        return true;
    }

    public async Task<WardrobeItem?> WearItemAsync(string userId, string itemId)
    {
        var existing = await repository.GetItemAsync(userId, itemId);
        if (existing is null)
            return null;

        await repository.MarkItemsWornAsync(userId, [itemId], UtcNow);
        return await repository.GetItemAsNoTrackingAsync(userId, itemId);
    }

    public async Task<WardrobeOperationResult<WardrobeItemImage?>> GetImageAsync(string userId, string itemId)
    {
        var existing = await repository.GetItemAsync(userId, itemId);
        if (existing is null)
            return WardrobeOperationResult<WardrobeItemImage?>.NotFound();

        var image = existing.HasImage ? await repository.GetImageAsync(itemId) : null;
        return WardrobeOperationResult<WardrobeItemImage?>.Ok(image);
    }

    public async Task<WardrobeItem?> SetImageAsync(string userId, string itemId, byte[] data, string contentType)
    {
        var existing = await repository.GetItemAsync(userId, itemId);
        if (existing is null)
            return null;

        await repository.SetImageAsync(existing, data, contentType);
        return existing;
    }

    public async Task<WardrobeItem?> DeleteImageAsync(string userId, string itemId)
    {
        var existing = await repository.GetItemAsync(userId, itemId);
        if (existing is null)
            return null;

        if (existing.HasImage)
            await repository.DeleteImageAsync(existing);

        return existing;
    }

    public WardrobeClassification Classify(string? name, string? notes, string? colorHex)
    {
        return WardrobeClassifier.Classify(name, notes, colorHex);
    }

    public async Task<OutfitSuggestionResult> SuggestOutfitsAsync(string userId, OutfitSuggestionRequest request)
    {
        var items = await repository.GetAllItemsAsync(userId);
        return OutfitGenerator.Generate(items, request, UtcNow);
    }

    public async Task<List<WardrobeOutfitDetails>> GetOutfitsAsync(string userId)
    {
        var outfits = await repository.GetOutfitsAsync(userId);
        var items = (await repository.GetAllItemsAsync(userId)).ToDictionary(x => x.Id);

        return outfits
            .Select(outfit => new WardrobeOutfitDetails
            {
                Outfit = outfit,
                Items = outfit.ItemIds.Where(items.ContainsKey).Select(id => items[id]).ToList()
            })
            // Образ, у которого после удаления вещей осталось меньше двух, не показываем.
            .Where(x => x.Items.Count >= 2)
            .ToList();
    }

    public async Task<WardrobeOperationResult<WardrobeOutfitDetails>> CreateOutfitAsync(string userId, WardrobeOutfit outfit)
    {
        var itemIds = outfit.ItemIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var items = await repository.GetItemsByIdsAsync(userId, itemIds);
        if (itemIds.Count < 2 || items.Count != itemIds.Count)
            return WardrobeOperationResult<WardrobeOutfitDetails>.InvalidItems();

        var now = UtcNow;
        outfit.Id = Guid.NewGuid().ToString();
        outfit.UserId = userId;
        outfit.ItemIds = itemIds;
        outfit.Name = outfit.Name.Trim();
        outfit.Note = string.IsNullOrWhiteSpace(outfit.Note) ? null : outfit.Note.Trim();
        outfit.WearCount = 0;
        outfit.LastWornAt = null;
        outfit.CreatedAt = now;
        outfit.UpdatedAt = now;

        if (!await repository.AddOutfitAsync(outfit))
            return WardrobeOperationResult<WardrobeOutfitDetails>.InvalidItems();

        var byId = items.ToDictionary(x => x.Id);
        return WardrobeOperationResult<WardrobeOutfitDetails>.Ok(new WardrobeOutfitDetails
        {
            Outfit = outfit,
            Items = itemIds.Select(id => byId[id]).ToList()
        });
    }

    public async Task<bool> DeleteOutfitAsync(string userId, string outfitId)
    {
        var outfit = await repository.GetOutfitAsync(userId, outfitId);
        if (outfit is null)
            return false;

        await repository.DeleteOutfitAsync(outfit);
        return true;
    }

    public async Task<WardrobeOutfitDetails?> WearOutfitAsync(string userId, string outfitId)
    {
        var outfit = await repository.GetOutfitAsync(userId, outfitId);
        if (outfit is null)
            return null;

        var now = UtcNow;
        await repository.MarkOutfitWornAsync(userId, outfitId, now);
        await repository.MarkItemsWornAsync(userId, outfit.ItemIds, now);

        var updated = await repository.GetOutfitAsNoTrackingAsync(userId, outfitId) ?? outfit;
        var items = (await repository.GetItemsByIdsAsNoTrackingAsync(userId, updated.ItemIds)).ToDictionary(x => x.Id);
        return new WardrobeOutfitDetails
        {
            Outfit = updated,
            Items = updated.ItemIds.Where(items.ContainsKey).Select(id => items[id]).ToList()
        };
    }

    private static void Normalize(WardrobeItem item)
    {
        item.Name = item.Name.Trim();
        item.Brand = string.IsNullOrWhiteSpace(item.Brand) ? null : item.Brand.Trim();
        item.Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();
        item.Colors = NormalizeCodes(item.Colors);
        item.Seasons = NormalizeCodes(item.Seasons);
        item.Styles = NormalizeCodes(item.Styles);
        item.Warmth = Math.Clamp(item.Warmth, 1, 5);
    }

    private static List<string> NormalizeCodes(IEnumerable<string> values)
    {
        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
    }
}
