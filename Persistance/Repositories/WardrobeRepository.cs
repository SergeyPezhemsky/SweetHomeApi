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
        var now = DateTime.UtcNow;
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Порядок блокировок: образы пользователя → фото вещи (загрузка фото берёт только вторую),
        // поэтому взаимной блокировки с параллельной загрузкой фото нет.
        await LockUserOutfitsAsync(item.UserId);
        await LockItemImageAsync(item.Id);

        // Всё — прямыми SQL-командами: нет отслеживаемых сущностей, которые мог бы «устареть» параллельный запрос.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WardrobeOutfits"
            SET "ItemIds" = array_remove("ItemIds", {item.Id}), "UpdatedAt" = {now}
            WHERE "UserId" = {item.UserId} AND {item.Id} = ANY("ItemIds")
            """);
        await context.WardrobeOutfits
            .Where(x => x.UserId == item.UserId && x.ItemIds.Count < 2)
            .ExecuteDeleteAsync();
        await context.WardrobeItemImages.Where(x => x.ItemId == item.Id).ExecuteDeleteAsync();
        await context.WardrobeItems.Where(x => x.Id == item.Id && x.UserId == item.UserId).ExecuteDeleteAsync();

        await transaction.CommitAsync();
        context.Entry(item).State = EntityState.Detached;
    }

    public async Task<WardrobeImageContent?> GetImageAsync(string itemId, WardrobeImageVariant variant)
    {
        // Проекция на один столбец: оригинал и обработанная версия не грузятся вместе.
        if (variant == WardrobeImageVariant.PROCESSED)
        {
            var processed = await context.WardrobeItemImages
                .AsNoTracking()
                .Where(x => x.ItemId == itemId && x.ProcessedData != null && x.ProcessingStatus == WardrobeImageStatus.READY)
                .Select(x => new { x.ProcessedData, x.ProcessedContentType })
                .FirstOrDefaultAsync();
            return processed is null ? null : new WardrobeImageContent(processed.ProcessedData!, processed.ProcessedContentType ?? "image/jpeg");
        }

        var original = await context.WardrobeItemImages
            .AsNoTracking()
            .Where(x => x.ItemId == itemId)
            .Select(x => new { x.Data, x.ContentType })
            .FirstOrDefaultAsync();
        return original is null ? null : new WardrobeImageContent(original.Data, original.ContentType);
    }

    public async Task<bool> SetImageAsync(WardrobeItem item, byte[] data, string contentType, bool queueProcessing)
    {
        var now = Now();
        var status = queueProcessing ? WardrobeImageStatus.PENDING : WardrobeImageStatus.NONE;
        var statusText = status.ToString();

        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(item.Id);

        try
        {
            // Upsert одной командой: две одновременные первые загрузки не падают на первичном ключе.
            // Новая ревизия делает недействительным результат обработки предыдущего фото.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WardrobeItemImages"
                    ("ItemId", "Data", "ContentType", "UpdatedAt", "ProcessedData", "ProcessedContentType", "ProcessingStatus", "Revision")
                VALUES ({item.Id}, {data}, {contentType}, {now}, NULL, NULL, {statusText}, 1)
                ON CONFLICT ("ItemId") DO UPDATE
                SET "Data" = EXCLUDED."Data",
                    "ContentType" = EXCLUDED."ContentType",
                    "UpdatedAt" = EXCLUDED."UpdatedAt",
                    "ProcessedData" = NULL,
                    "ProcessedContentType" = NULL,
                    "ProcessingStatus" = EXCLUDED."ProcessingStatus",
                    "Revision" = "WardrobeItemImages"."Revision" + 1
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
                .SetProperty(x => x.ImageStatus, status)
                .SetProperty(x => x.ImageVariant, WardrobeImageVariant.ORIGINAL)
                .SetProperty(x => x.UpdatedAt, now));
        if (updated == 0)
            return false;

        await transaction.CommitAsync();
        item.HasImage = true;
        item.ImageUpdatedAt = now;
        item.ImageStatus = status;
        item.ImageVariant = WardrobeImageVariant.ORIGINAL;
        item.UpdatedAt = now;
        return true;
    }

    public async Task DeleteImageAsync(WardrobeItem item)
    {
        var now = Now();

        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(item.Id);

        await context.WardrobeItemImages.Where(x => x.ItemId == item.Id).ExecuteDeleteAsync();
        await context.WardrobeItems
            .Where(x => x.Id == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.HasImage, false)
                .SetProperty(x => x.ImageUpdatedAt, (DateTime?)null)
                .SetProperty(x => x.ImageStatus, WardrobeImageStatus.NONE)
                .SetProperty(x => x.ImageVariant, WardrobeImageVariant.ORIGINAL)
                .SetProperty(x => x.UpdatedAt, now));

        await transaction.CommitAsync();
        item.HasImage = false;
        item.ImageUpdatedAt = null;
        item.ImageStatus = WardrobeImageStatus.NONE;
        item.ImageVariant = WardrobeImageVariant.ORIGINAL;
        item.UpdatedAt = now;
    }

    public async Task<bool> SetImageVariantAsync(WardrobeItem item, WardrobeImageVariant variant)
    {
        var now = Now();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(item.Id);

        if (variant == WardrobeImageVariant.PROCESSED
            && !await context.WardrobeItemImages.AnyAsync(x => x.ItemId == item.Id && x.ProcessingStatus == WardrobeImageStatus.READY))
            return false;

        var updated = await context.WardrobeItems
            .Where(x => x.Id == item.Id && x.HasImage)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ImageVariant, variant)
                // Другая картинка по тому же URL: меняем ключ кэша на клиенте.
                .SetProperty(x => x.ImageUpdatedAt, now)
                .SetProperty(x => x.UpdatedAt, now));
        if (updated == 0)
            return false;

        await transaction.CommitAsync();
        item.ImageVariant = variant;
        item.ImageUpdatedAt = now;
        item.UpdatedAt = now;
        return true;
    }

    public async Task<bool> MarkImagePendingAsync(WardrobeItem item)
    {
        var now = Now();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(item.Id);

        // Повтор — новая ревизия: если старая обработка ещё идёт, её результат будет отброшен.
        var images = await context.WardrobeItemImages
            .Where(x => x.ItemId == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, WardrobeImageStatus.PENDING)
                .SetProperty(x => x.ProcessedData, (byte[]?)null)
                .SetProperty(x => x.ProcessedContentType, (string?)null)
                .SetProperty(x => x.Revision, x => x.Revision + 1));
        if (images == 0)
            return false;

        await context.WardrobeItems
            .Where(x => x.Id == item.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ImageStatus, WardrobeImageStatus.PENDING)
                .SetProperty(x => x.ImageVariant, WardrobeImageVariant.ORIGINAL)
                .SetProperty(x => x.ImageUpdatedAt, now)
                .SetProperty(x => x.UpdatedAt, now));

        await transaction.CommitAsync();
        item.ImageStatus = WardrobeImageStatus.PENDING;
        item.ImageVariant = WardrobeImageVariant.ORIGINAL;
        item.ImageUpdatedAt = now;
        item.UpdatedAt = now;
        return true;
    }

    public Task<List<string>> GetPendingImageItemIdsAsync()
    {
        return context.WardrobeItemImages
            .AsNoTracking()
            .Where(x => x.ProcessingStatus == WardrobeImageStatus.PENDING)
            .OrderBy(x => x.UpdatedAt)
            .Select(x => x.ItemId)
            .ToListAsync();
    }

    public async Task<WardrobePendingImage?> GetPendingImageAsync(string itemId)
    {
        var image = await context.WardrobeItemImages
            .AsNoTracking()
            .Where(x => x.ItemId == itemId && x.ProcessingStatus == WardrobeImageStatus.PENDING)
            .Select(x => new { x.ItemId, x.Data, x.ContentType, x.Revision })
            .FirstOrDefaultAsync();
        return image is null ? null : new WardrobePendingImage(image.ItemId, image.Data, image.ContentType, image.Revision);
    }

    public async Task<bool> SaveProcessedImageAsync(string itemId, int revision, byte[] data, string contentType)
    {
        var now = Now();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(itemId);

        // Только для той же ревизии: если за время обработки загрузили новое фото или удалили вещь, результат не нужен.
        var images = await context.WardrobeItemImages
            .Where(x => x.ItemId == itemId && x.Revision == revision && x.ProcessingStatus == WardrobeImageStatus.PENDING)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessedData, data)
                .SetProperty(x => x.ProcessedContentType, contentType)
                .SetProperty(x => x.ProcessingStatus, WardrobeImageStatus.READY));
        if (images == 0)
            return false;

        await context.WardrobeItems
            .Where(x => x.Id == itemId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ImageStatus, WardrobeImageStatus.READY)
                .SetProperty(x => x.ImageVariant, WardrobeImageVariant.PROCESSED)
                .SetProperty(x => x.ImageUpdatedAt, now)
                .SetProperty(x => x.UpdatedAt, now));

        await transaction.CommitAsync();
        return true;
    }

    public async Task MarkImageProcessingFailedAsync(string itemId, int revision)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockItemImageAsync(itemId);

        var images = await context.WardrobeItemImages
            .Where(x => x.ItemId == itemId && x.Revision == revision && x.ProcessingStatus == WardrobeImageStatus.PENDING)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ProcessingStatus, WardrobeImageStatus.FAILED));
        if (images == 0)
            return;

        await context.WardrobeItems
            .Where(x => x.Id == itemId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ImageStatus, WardrobeImageStatus.FAILED));

        await transaction.CommitAsync();
    }

    private static DateTime Now()
    {
        // PostgreSQL хранит время с точностью до микросекунд: обрезаем заранее, чтобы ответ
        // и последующие чтения отдавали одинаковое время (по imageUpdatedAt клиент кэширует фото).
        var utcNow = DateTime.UtcNow;
        return new DateTime(utcNow.Ticks - utcNow.Ticks % 10, DateTimeKind.Utc);
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

    public async Task<WardrobeOperationStatus> UpdateOutfitAsync(WardrobeOutfit outfit)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockUserOutfitsAsync(outfit.UserId);

        var ids = outfit.ItemIds.ToList();
        var existing = await context.WardrobeItems.CountAsync(x => x.UserId == outfit.UserId && ids.Contains(x.Id));
        if (existing != ids.Count)
            return WardrobeOperationStatus.InvalidItems;

        var updated = await context.WardrobeOutfits
            .Where(x => x.Id == outfit.Id && x.UserId == outfit.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Name, outfit.Name)
                .SetProperty(x => x.ItemIds, ids)
                .SetProperty(x => x.Occasion, outfit.Occasion)
                .SetProperty(x => x.Temperature, outfit.Temperature)
                .SetProperty(x => x.Note, outfit.Note)
                .SetProperty(x => x.UpdatedAt, outfit.UpdatedAt));
        if (updated == 0)
            return WardrobeOperationStatus.NotFound;

        await transaction.CommitAsync();
        return WardrobeOperationStatus.Ok;
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
        await context.WardrobeOutfits
            .Where(x => x.Id == outfit.Id && x.UserId == outfit.UserId)
            .ExecuteDeleteAsync();
        context.Entry(outfit).State = EntityState.Detached;
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
