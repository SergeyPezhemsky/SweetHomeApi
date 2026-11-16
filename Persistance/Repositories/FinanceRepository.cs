using Application.Modules.Finance;
using Microsoft.EntityFrameworkCore;

namespace Persistance.Repositories;

public class FinanceRepository(SweetHomeDbContext context) : IFinanceRepository
{
    public Task<int> NextRevisionAsync()
    {
        // Общая последовательность, а не «+1»: номер не повторится даже после удаления и повторного создания строки.
        return context.Database
            .SqlQueryRaw<int>("""SELECT nextval('"FinanceReceiptRevisionSeq"')::integer AS "Value" """)
            .SingleAsync();
    }

    public async Task AddReceiptAsync(FinanceReceipt receipt, FinanceReceiptImage? image)
    {
        context.FinanceReceipts.Add(receipt);
        if (image is not null)
            context.FinanceReceiptImages.Add(image);

        await context.SaveChangesAsync();

        context.Entry(receipt).State = EntityState.Detached;
        foreach (var item in receipt.Items)
            context.Entry(item).State = EntityState.Detached;
        if (image is not null)
            context.Entry(image).State = EntityState.Detached;
    }

    public async Task<List<FinanceReceipt>> GetReceiptsAsync(string userId, DateOnly from, DateOnly toExclusive)
    {
        var receipts = await context.FinanceReceipts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Items)
            .Where(x => x.UserId == userId && x.PurchasedOn >= from && x.PurchasedOn < toExclusive)
            .ToListAsync();

        foreach (var receipt in receipts)
            receipt.Items = receipt.Items.OrderBy(x => x.Position).ToList();

        return receipts;
    }

    public async Task<FinanceReceipt?> GetReceiptAsync(string userId, string receiptId)
    {
        var receipt = await context.FinanceReceipts
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == receiptId);

        if (receipt is not null)
            receipt.Items = receipt.Items.OrderBy(x => x.Position).ToList();

        return receipt;
    }

    public Task<FinanceReceiptImage?> GetImageAsync(string userId, string receiptId)
    {
        return context.FinanceReceiptImages
            .AsNoTracking()
            .Where(x => x.ReceiptId == receiptId && x.Receipt.UserId == userId)
            .Select(x => new FinanceReceiptImage { ReceiptId = x.ReceiptId, Data = x.Data, ContentType = x.ContentType })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ReplaceReceiptAsync(string userId, string receiptId, FinanceReceiptUpdate update,
        List<FinanceReceiptItem> items, int revision, DateTime updatedAt)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockReceiptAsync(receiptId);

        // Новая ревизия: результат распознавания, которое могло ещё идти, будет отброшен.
        var updated = await context.FinanceReceipts
            .Where(x => x.Id == receiptId && x.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PurchasedOn, update.PurchasedOn)
                .SetProperty(x => x.Store, update.Store)
                .SetProperty(x => x.Total, update.Total)
                .SetProperty(x => x.Currency, update.Currency)
                .SetProperty(x => x.Category, update.Category)
                .SetProperty(x => x.Note, update.Note)
                .SetProperty(x => x.Status, FinanceReceiptStatus.READY)
                .SetProperty(x => x.FailureReason, (FinanceFailureReason?)null)
                // Модель остаётся, только если она действительно прочитала чек (READY); нераспознанный или ещё
                // распознаваемый чек, заполненный руками, — заслуга пользователя, а не модели.
                .SetProperty(x => x.RecognitionModel, x => x.Status == FinanceReceiptStatus.READY ? x.RecognitionModel : null)
                .SetProperty(x => x.Revision, revision)
                .SetProperty(x => x.UpdatedAt, updatedAt));
        if (updated == 0)
            return false;

        await ReplaceItemsAsync(receiptId, items);
        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteReceiptAsync(string userId, string receiptId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockReceiptAsync(receiptId);

        // Позиции и фото удаляются каскадом внешних ключей.
        var deleted = await context.FinanceReceipts
            .Where(x => x.Id == receiptId && x.UserId == userId)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
        return deleted > 0;
    }

    public async Task<FinanceOperationStatus> MarkPendingAsync(string userId, string receiptId, int revision, DateTime updatedAt)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockReceiptAsync(receiptId);

        var receipt = await context.FinanceReceipts
            .Where(x => x.Id == receiptId && x.UserId == userId)
            .Select(x => new { x.HasImage, x.Source })
            .FirstOrDefaultAsync();
        if (receipt is null)
            return FinanceOperationStatus.NotFound;
        if (receipt.Source == FinanceReceiptSource.BANK_SCREENSHOT)
            return FinanceOperationStatus.AlreadyImported;
        if (!receipt.HasImage)
            return FinanceOperationStatus.NoImage;

        await context.FinanceReceipts
            .Where(x => x.Id == receiptId && x.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, FinanceReceiptStatus.PENDING)
                .SetProperty(x => x.FailureReason, (FinanceFailureReason?)null)
                .SetProperty(x => x.Revision, revision)
                .SetProperty(x => x.UpdatedAt, updatedAt));

        await transaction.CommitAsync();
        return FinanceOperationStatus.Ok;
    }

    public Task<List<string>> GetPendingReceiptIdsAsync()
    {
        return context.FinanceReceipts
            .AsNoTracking()
            .Where(x => x.Status == FinanceReceiptStatus.PENDING)
            .OrderBy(x => x.UpdatedAt)
            .Select(x => x.Id)
            .ToListAsync();
    }

    public async Task<FinancePendingRecognition?> GetPendingRecognitionAsync(string receiptId)
    {
        var pending = await context.FinanceReceiptImages
            .AsNoTracking()
            .Where(x => x.ReceiptId == receiptId && x.Receipt.Status == FinanceReceiptStatus.PENDING)
            .Select(x => new
            {
                x.ReceiptId,
                x.Receipt.UserId,
                x.Receipt.Revision,
                x.Receipt.UploadedOn,
                x.Receipt.PurchasedOn,
                x.Data,
                x.ContentType
            })
            .FirstOrDefaultAsync();

        return pending is null
            ? null
            : new FinancePendingRecognition(pending.ReceiptId, pending.UserId, pending.Revision, pending.UploadedOn,
                pending.PurchasedOn, pending.Data, pending.ContentType);
    }

    public async Task<bool> CompleteRecognitionAsync(FinancePendingRecognition pending, FinanceRecognitionBatch batch, DateTime updatedAt)
    {
        var receiptId = pending.ReceiptId;
        var result = batch.Receipts[0];
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockReceiptAsync(receiptId);

        var updated = await context.FinanceReceipts
            .Where(x => x.Id == receiptId && x.UserId == pending.UserId && x.Revision == pending.Revision
                        && x.Status == FinanceReceiptStatus.PENDING)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PurchasedOn, result.PurchasedOn)
                .SetProperty(x => x.Store, result.Store)
                .SetProperty(x => x.Total, result.Total)
                .SetProperty(x => x.Currency, result.Currency)
                .SetProperty(x => x.Category, result.Category)
                .SetProperty(x => x.Status, FinanceReceiptStatus.READY)
                .SetProperty(x => x.FailureReason, (FinanceFailureReason?)null)
                .SetProperty(x => x.Source, x => batch.IsBankScreenshot ? FinanceReceiptSource.BANK_SCREENSHOT : x.Source)
                .SetProperty(x => x.RecognitionJson, batch.RawJson)
                .SetProperty(x => x.RecognitionModel, batch.Model)
                .SetProperty(x => x.UpdatedAt, updatedAt));
        if (updated == 0)
            return false;

        await ReplaceItemsAsync(receiptId, result.Items);
        if (batch.Receipts.Count > 1)
        {
            // Снимок хранится один раз в исходной записи. Покупки можно удалять и исправлять по отдельности.
            var additional = new List<FinanceReceipt>(batch.Receipts.Count - 1);
            foreach (var purchase in batch.Receipts.Skip(1))
                additional.Add(batch.CreateAdditionalReceipt(pending, purchase, await NextRevisionAsync(), updatedAt));
            context.FinanceReceipts.AddRange(additional);
            await context.SaveChangesAsync();
            foreach (var purchase in additional)
                context.Entry(purchase).State = EntityState.Detached;
        }
        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> FailRecognitionAsync(string receiptId, int revision, FinanceFailureReason reason, string? rawJson,
        string? model, DateTime updatedAt)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LockReceiptAsync(receiptId);

        var updated = await context.FinanceReceipts
            .Where(x => x.Id == receiptId && x.Revision == revision && x.Status == FinanceReceiptStatus.PENDING)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, FinanceReceiptStatus.FAILED)
                .SetProperty(x => x.FailureReason, reason)
                // Ответ и модель — от этой попытки; никто не ответил — оба пустые, чтобы не приписать старый ответ.
                .SetProperty(x => x.RecognitionJson, rawJson)
                .SetProperty(x => x.RecognitionModel, model)
                .SetProperty(x => x.UpdatedAt, updatedAt));

        await transaction.CommitAsync();
        return updated > 0;
    }

    public Task<Dictionary<string, FinanceCategory>> GetRulesAsync(string userId, IReadOnlyCollection<string> keys)
    {
        var list = keys.ToList();
        return context.FinanceCategoryRules
            .AsNoTracking()
            .Where(x => x.UserId == userId && list.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Category);
    }

    public async Task UpsertRulesAsync(string userId, IReadOnlyDictionary<string, FinanceCategory> rules, DateTime updatedAt)
    {
        foreach (var (key, category) in rules)
        {
            var code = category.ToString();
            // Upsert одной командой: две параллельные правки не падают на первичном ключе.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "FinanceCategoryRules" ("UserId", "Key", "Category", "UpdatedAt")
                VALUES ({userId}, {key}, {code}, {updatedAt})
                ON CONFLICT ("UserId", "Key") DO UPDATE
                SET "Category" = EXCLUDED."Category", "UpdatedAt" = EXCLUDED."UpdatedAt"
                """);
        }
    }

    private async Task ReplaceItemsAsync(string receiptId, List<FinanceReceiptItem> items)
    {
        await context.FinanceReceiptItems.Where(x => x.ReceiptId == receiptId).ExecuteDeleteAsync();
        if (items.Count == 0)
            return;

        foreach (var item in items)
            item.ReceiptId = receiptId;

        context.FinanceReceiptItems.AddRange(items);
        await context.SaveChangesAsync();
        foreach (var item in items)
            context.Entry(item).State = EntityState.Detached;
    }

    /// <summary>Сериализует правку, удаление и сохранение результата распознавания одного чека.</summary>
    private Task LockReceiptAsync(string receiptId)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"finance-receipt:" + receiptId}))");
    }
}
