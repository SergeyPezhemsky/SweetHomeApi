using Application.Modules.Finance;

namespace SweetHomeApi.Tests.Finance;

/// <summary>Репозиторий в памяти с той же семантикой ревизий и статусов, что и SQL-версия.</summary>
internal sealed class FakeFinanceRepository : IFinanceRepository
{
    private int _revision;

    public Dictionary<string, FinanceReceipt> Receipts { get; } = [];
    public Dictionary<string, FinanceReceiptImage> Images { get; } = [];
    public Dictionary<(string UserId, string Key), FinanceCategory> Rules { get; } = [];

    /// <summary>Имитация сбоя базы при сохранении результата распознавания.</summary>
    public bool FailNextComplete { get; set; }

    public Task<int> NextRevisionAsync() => Task.FromResult(++_revision);

    public Task AddReceiptAsync(FinanceReceipt receipt, FinanceReceiptImage? image)
    {
        Receipts[receipt.Id] = Clone(receipt);
        if (image is not null)
            Images[receipt.Id] = image;
        return Task.CompletedTask;
    }

    public Task<List<FinanceReceipt>> GetReceiptsAsync(string userId, DateOnly from, DateOnly toExclusive)
    {
        return Task.FromResult(Receipts.Values
            .Where(x => x.UserId == userId && x.PurchasedOn >= from && x.PurchasedOn < toExclusive)
            .Select(Clone)
            .ToList());
    }

    public Task<FinanceReceipt?> GetReceiptAsync(string userId, string receiptId)
    {
        return Task.FromResult(Receipts.TryGetValue(receiptId, out var receipt) && receipt.UserId == userId ? Clone(receipt) : null);
    }

    public Task<FinanceReceiptImage?> GetImageAsync(string userId, string receiptId)
    {
        return Task.FromResult(Receipts.TryGetValue(receiptId, out var receipt) && receipt.UserId == userId
            ? Images.GetValueOrDefault(receiptId)
            : null);
    }

    public Task<bool> ReplaceReceiptAsync(string userId, string receiptId, FinanceReceiptUpdate update, List<FinanceReceiptItem> items,
        int revision, DateTime updatedAt)
    {
        if (!Receipts.TryGetValue(receiptId, out var receipt) || receipt.UserId != userId)
            return Task.FromResult(false);

        receipt.PurchasedOn = update.PurchasedOn;
        receipt.Store = update.Store;
        receipt.Total = update.Total;
        receipt.Currency = update.Currency;
        receipt.Category = update.Category;
        receipt.Note = update.Note;
        receipt.Status = FinanceReceiptStatus.READY;
        receipt.FailureReason = null;
        receipt.Revision = revision;
        receipt.UpdatedAt = updatedAt;
        receipt.Items = items.Select(CloneItem).ToList();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteReceiptAsync(string userId, string receiptId)
    {
        if (!Receipts.TryGetValue(receiptId, out var receipt) || receipt.UserId != userId)
            return Task.FromResult(false);

        Receipts.Remove(receiptId);
        Images.Remove(receiptId);
        return Task.FromResult(true);
    }

    public Task<FinanceOperationStatus> MarkPendingAsync(string userId, string receiptId, int revision, DateTime updatedAt)
    {
        if (!Receipts.TryGetValue(receiptId, out var receipt) || receipt.UserId != userId)
            return Task.FromResult(FinanceOperationStatus.NotFound);
        if (!receipt.HasImage)
            return Task.FromResult(FinanceOperationStatus.NoImage);

        receipt.Status = FinanceReceiptStatus.PENDING;
        receipt.FailureReason = null;
        receipt.Revision = revision;
        receipt.UpdatedAt = updatedAt;
        return Task.FromResult(FinanceOperationStatus.Ok);
    }

    public Task<List<string>> GetPendingReceiptIdsAsync()
    {
        return Task.FromResult(Receipts.Values.Where(x => x.Status == FinanceReceiptStatus.PENDING).Select(x => x.Id).ToList());
    }

    public Task<FinancePendingRecognition?> GetPendingRecognitionAsync(string receiptId)
    {
        if (!Receipts.TryGetValue(receiptId, out var receipt) || receipt.Status != FinanceReceiptStatus.PENDING
            || !Images.TryGetValue(receiptId, out var image))
            return Task.FromResult<FinancePendingRecognition?>(null);

        return Task.FromResult<FinancePendingRecognition?>(new FinancePendingRecognition(
            receipt.Id, receipt.UserId, receipt.Revision, receipt.UploadedOn, image.Data, image.ContentType));
    }

    public Task<bool> CompleteRecognitionAsync(string receiptId, int revision, FinanceRecognitionResult result, DateTime updatedAt)
    {
        if (FailNextComplete)
        {
            FailNextComplete = false;
            throw new InvalidOperationException("database is down");
        }

        if (!Receipts.TryGetValue(receiptId, out var receipt) || receipt.Revision != revision
            || receipt.Status != FinanceReceiptStatus.PENDING)
            return Task.FromResult(false);

        receipt.PurchasedOn = result.PurchasedOn;
        receipt.Store = result.Store;
        receipt.Total = result.Total;
        receipt.Currency = result.Currency;
        receipt.Category = result.Category;
        receipt.Status = FinanceReceiptStatus.READY;
        receipt.FailureReason = null;
        receipt.RecognitionJson = result.RawJson;
        receipt.UpdatedAt = updatedAt;
        receipt.Items = result.Items.Select(CloneItem).ToList();
        return Task.FromResult(true);
    }

    public Task<bool> FailRecognitionAsync(string receiptId, int revision, FinanceFailureReason reason, string? rawJson, DateTime updatedAt)
    {
        if (!Receipts.TryGetValue(receiptId, out var receipt) || receipt.Revision != revision
            || receipt.Status != FinanceReceiptStatus.PENDING)
            return Task.FromResult(false);

        receipt.Status = FinanceReceiptStatus.FAILED;
        receipt.FailureReason = reason;
        receipt.RecognitionJson = rawJson ?? receipt.RecognitionJson;
        receipt.UpdatedAt = updatedAt;
        return Task.FromResult(true);
    }

    public Task<Dictionary<string, FinanceCategory>> GetRulesAsync(string userId, IReadOnlyCollection<string> keys)
    {
        return Task.FromResult(Rules
            .Where(x => x.Key.UserId == userId && keys.Contains(x.Key.Key))
            .ToDictionary(x => x.Key.Key, x => x.Value));
    }

    public Task UpsertRulesAsync(string userId, IReadOnlyDictionary<string, FinanceCategory> rules, DateTime updatedAt)
    {
        foreach (var (key, category) in rules)
            Rules[(userId, key)] = category;
        return Task.CompletedTask;
    }

    private static FinanceReceipt Clone(FinanceReceipt receipt)
    {
        var copy = (FinanceReceipt)receipt.GetType()
            .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(receipt, null)!;
        copy.Items = receipt.Items.Select(CloneItem).ToList();
        return copy;
    }

    private static FinanceReceiptItem CloneItem(FinanceReceiptItem item)
    {
        return new FinanceReceiptItem
        {
            Id = item.Id,
            ReceiptId = item.ReceiptId,
            Position = item.Position,
            Name = item.Name,
            Quantity = item.Quantity,
            Price = item.Price,
            Amount = item.Amount,
            Category = item.Category,
            CategorySource = item.CategorySource
        };
    }
}

internal sealed class FakeRecognitionQueue(bool enabled) : IFinanceRecognitionQueue
{
    public List<string> Items { get; } = [];
    public bool IsEnabled => enabled;
    public void Enqueue(string receiptId) => Items.Add(receiptId);
}

internal sealed class FakeRecognizer(Func<int, Task<string>> respond) : IReceiptRecognizer
{
    public int Calls { get; private set; }
    public bool IsEnabled => true;

    public Task<string> RecognizeAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        return respond(++Calls);
    }
}
