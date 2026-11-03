using Application.Modules.Wardrobe;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class WardrobeRepository(SweetHomeDbContext context) : IWardrobeRepository
{
    public async Task<List<WardrobeItem>> GetItemsAsync(string userId, WardrobeItemQuery query)
    {
        var items = context.WardrobeItems
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var search = $"%{EscapeLike(query.Query.Trim())}%";
            items = items.Where(x =>
                EF.Functions.ILike(x.Name, search, "\\")
                || (x.Brand != null && EF.Functions.ILike(x.Brand, search, "\\"))
                || (x.Notes != null && EF.Functions.ILike(x.Notes, search, "\\")));
        }

        if (query.Category.HasValue)
            items = items.Where(x => x.Category == query.Category.Value);

        if (!string.IsNullOrWhiteSpace(query.Season))
        {
            var season = query.Season;
            // Всесезонные вещи (пустой список) подходят к любому сезону.
            items = items.Where(x => x.Seasons.Count == 0 || x.Seasons.Contains(season));
        }

        if (!string.IsNullOrWhiteSpace(query.Style))
        {
            var style = query.Style;
            items = items.Where(x => x.Styles.Contains(style));
        }

        if (!string.IsNullOrWhiteSpace(query.Color))
        {
            var color = query.Color;
            items = items.Where(x => x.Colors.Contains(color));
        }

        if (query.Favorite.HasValue)
            items = items.Where(x => x.IsFavorite == query.Favorite.Value);

        return await items
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Dictionary<WardrobeCategory, int>> GetCategoryCountsAsync(string userId)
    {
        return await context.WardrobeItems
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.Category)
            .Select(x => new { Category = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count);
    }

    public Task<List<WardrobeItem>> GetAllItemsAsync(string userId)
    {
        return context.WardrobeItems
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task<List<WardrobeItem>> GetItemsByIdsAsync(string userId, IReadOnlyCollection<string> itemIds)
    {
        var ids = itemIds.ToList();
        return context.WardrobeItems
            .Where(x => x.UserId == userId && ids.Contains(x.Id))
            .ToListAsync();
    }

    public Task<WardrobeItem?> GetItemAsync(string userId, string itemId)
    {
        return context.WardrobeItems
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == itemId);
    }

    public async Task AddItemAsync(WardrobeItem item)
    {
        context.WardrobeItems.Add(item);
        await context.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(WardrobeItem item)
    {
        await context.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(WardrobeItem item)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var outfits = await context.WardrobeOutfits
            .Where(x => x.UserId == item.UserId && x.ItemIds.Contains(item.Id))
            .ToListAsync();

        foreach (var outfit in outfits)
        {
            outfit.ItemIds = outfit.ItemIds.Where(x => x != item.Id).ToList();
            outfit.UpdatedAt = DateTime.UtcNow;
            if (outfit.ItemIds.Count < 2)
                context.WardrobeOutfits.Remove(outfit);
        }

        var image = await context.WardrobeItemImages.FirstOrDefaultAsync(x => x.ItemId == item.Id);
        if (image is not null)
            context.WardrobeItemImages.Remove(image);

        context.WardrobeItems.Remove(item);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public Task<WardrobeItemImage?> GetImageAsync(string itemId)
    {
        return context.WardrobeItemImages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemId == itemId);
    }

    public async Task SetImageAsync(WardrobeItem item, byte[] data, string contentType)
    {
        var now = DateTime.UtcNow;
        var image = await context.WardrobeItemImages.FirstOrDefaultAsync(x => x.ItemId == item.Id);
        if (image is null)
        {
            image = new WardrobeItemImage { ItemId = item.Id };
            context.WardrobeItemImages.Add(image);
        }

        image.Data = data;
        image.ContentType = contentType;
        image.UpdatedAt = now;

        item.HasImage = true;
        item.ImageUpdatedAt = now;
        item.UpdatedAt = now;

        await context.SaveChangesAsync();
    }

    public async Task DeleteImageAsync(WardrobeItem item)
    {
        var image = await context.WardrobeItemImages.FirstOrDefaultAsync(x => x.ItemId == item.Id);
        if (image is not null)
            context.WardrobeItemImages.Remove(image);

        item.HasImage = false;
        item.ImageUpdatedAt = null;
        item.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public Task<List<WardrobeOutfit>> GetOutfitsAsync(string userId)
    {
        return context.WardrobeOutfits
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public Task<WardrobeOutfit?> GetOutfitAsync(string userId, string outfitId)
    {
        return context.WardrobeOutfits
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == outfitId);
    }

    public async Task AddOutfitAsync(WardrobeOutfit outfit)
    {
        context.WardrobeOutfits.Add(outfit);
        await context.SaveChangesAsync();
    }

    public async Task DeleteOutfitAsync(WardrobeOutfit outfit)
    {
        context.WardrobeOutfits.Remove(outfit);
        await context.SaveChangesAsync();
    }

    public Task SaveChangesAsync()
    {
        return context.SaveChangesAsync();
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
