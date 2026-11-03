using Application.Modules.Wardrobe;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            // «ё» и «е» считаем одной буквой: «черн» находит «Чёрные брюки».
            var search = $"%{EscapeLike(FoldYo(query.Query.Trim()))}%";
            items = items.Where(x =>
                EF.Functions.ILike(x.Name.Replace("ё", "е").Replace("Ё", "Е"), search, "\\")
                || (x.Brand != null && EF.Functions.ILike(x.Brand.Replace("ё", "е").Replace("Ё", "Е"), search, "\\"))
                || (x.Notes != null && EF.Functions.ILike(x.Notes.Replace("ё", "е").Replace("Ё", "Е"), search, "\\")));
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

    public Task<WardrobeItem?> GetItemAsNoTrackingAsync(string userId, string itemId)
    {
        return context.WardrobeItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == itemId);
    }

    public Task<List<WardrobeItem>> GetItemsByIdsAsNoTrackingAsync(string userId, IReadOnlyCollection<string> itemIds)
    {
        var ids = itemIds.ToList();
        return context.WardrobeItems
            .AsNoTracking()
            .Where(x => x.UserId == userId && ids.Contains(x.Id))
            .ToListAsync();
    }

    public Task<WardrobeOutfit?> GetOutfitAsNoTrackingAsync(string userId, string outfitId)
    {
        return context.WardrobeOutfits
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == outfitId);
    }

    public async Task AddItemAsync(WardrobeItem item)
    {
        context.WardrobeItems.Add(item);
        await context.SaveChangesAsync();
    }

    public Task<bool> UpdateItemAsync(WardrobeItem item)
    {
        // Вещь могли удалить параллельно: тогда это 404, а не 500.
        return SaveIgnoringMissingRowsAsync();
    }

    public async Task DeleteItemAsync(WardrobeItem item)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockUserOutfitsAsync(item.UserId);

        // Параллельный запрос мог удалить вещь, пока мы ждали блокировку: тогда делать нечего.
        if (!await context.WardrobeItems.AnyAsync(x => x.Id == item.Id))
            return;

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
        if (await SaveIgnoringMissingRowsAsync())
            await transaction.CommitAsync();
    }

    public Task<WardrobeItemImage?> GetImageAsync(string itemId)
    {
        return context.WardrobeItemImages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemId == itemId);
    }

    public async Task<bool> SetImageAsync(WardrobeItem item, byte[] data, string contentType)
    {
        // PostgreSQL хранит время с точностью до микросекунд: обрезаем заранее, чтобы ответ на загрузку
        // и последующие чтения отдавали одинаковый imageUpdatedAt (по нему клиент кэширует фото).
        var utcNow = DateTime.UtcNow;
        var now = new DateTime(utcNow.Ticks - utcNow.Ticks % 10, DateTimeKind.Utc);

        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(item.Id);

        try
        {
            // Upsert одной командой: две одновременные первые загрузки не падают на первичном ключе.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WardrobeItemImages" ("ItemId", "Data", "ContentType", "UpdatedAt")
                VALUES ({item.Id}, {data}, {contentType}, {now})
                ON CONFLICT ("ItemId") DO UPDATE
                SET "Data" = EXCLUDED."Data", "ContentType" = EXCLUDED."ContentType", "UpdatedAt" = EXCLUDED."UpdatedAt"
                """);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // Вещь удалили параллельно — фото прикреплять не к чему.
            return false;
        }

        // Флаги обновляем тем же SQL-путём под той же блокировкой: фото и HasImage меняются согласованно.
        var updated = await context.WardrobeItems
            .Where(x => x.Id == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.HasImage, true)
                .SetProperty(x => x.ImageUpdatedAt, now)
                .SetProperty(x => x.UpdatedAt, now));
        if (updated == 0)
            return false;

        await transaction.CommitAsync();
        item.HasImage = true;
        item.ImageUpdatedAt = now;
        item.UpdatedAt = now;
        return true;
    }

    public async Task DeleteImageAsync(WardrobeItem item)
    {
        var utcNow = DateTime.UtcNow;
        var now = new DateTime(utcNow.Ticks - utcNow.Ticks % 10, DateTimeKind.Utc);

        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(item.Id);

        await context.WardrobeItemImages.Where(x => x.ItemId == item.Id).ExecuteDeleteAsync();
        await context.WardrobeItems
            .Where(x => x.Id == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.HasImage, false)
                .SetProperty(x => x.ImageUpdatedAt, (DateTime?)null)
                .SetProperty(x => x.UpdatedAt, now));

        await transaction.CommitAsync();
        item.HasImage = false;
        item.ImageUpdatedAt = null;
        item.UpdatedAt = now;
    }

    /// <summary>Сериализует загрузку и удаление фото одной вещи.</summary>
    private Task LockItemImageAsync(string itemId)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"wardrobe-image:" + itemId}))");
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

    public async Task<bool> AddOutfitAsync(WardrobeOutfit outfit)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockUserOutfitsAsync(outfit.UserId);

        // Повторная проверка под блокировкой: вещь могли удалить между проверкой в сервисе и вставкой.
        var ids = outfit.ItemIds.ToList();
        var existing = await context.WardrobeItems.CountAsync(x => x.UserId == outfit.UserId && ids.Contains(x.Id));
        if (existing != ids.Count)
            return false;

        context.WardrobeOutfits.Add(outfit);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    /// <summary>
    /// Сериализует изменения образов пользователя (удаление вещей и создание образов),
    /// чтобы параллельные запросы не теряли обновления массива ItemIds.
    /// </summary>
    private Task LockUserOutfitsAsync(string userId)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"wardrobe-outfits:" + userId}))");
    }

    public async Task DeleteOutfitAsync(WardrobeOutfit outfit)
    {
        context.WardrobeOutfits.Remove(outfit);
        await SaveIgnoringMissingRowsAsync();
    }

    public Task MarkItemsWornAsync(string userId, IReadOnlyCollection<string> itemIds, DateTime wornAt)
    {
        var ids = itemIds.ToList();
        return context.WardrobeItems
            .Where(x => x.UserId == userId && ids.Contains(x.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.WearCount, x => x.WearCount + 1)
                .SetProperty(x => x.LastWornAt, wornAt));
    }

    public Task MarkOutfitWornAsync(string userId, string outfitId, DateTime wornAt)
    {
        return context.WardrobeOutfits
            .Where(x => x.UserId == userId && x.Id == outfitId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.WearCount, x => x.WearCount + 1)
                .SetProperty(x => x.LastWornAt, wornAt)
                .SetProperty(x => x.UpdatedAt, wornAt));
    }

    /// <summary>
    /// Сохраняет изменения; если строку уже удалил параллельный запрос (двойной тап «Удалить»),
    /// это не ошибка: результат тот же — записи нет.
    /// </summary>
    private async Task<bool> SaveIgnoringMissingRowsAsync()
    {
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private static string FoldYo(string value) => value.Replace('ё', 'е').Replace('Ё', 'Е');

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
