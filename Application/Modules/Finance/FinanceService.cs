namespace Application.Modules.Finance;

public class FinanceService(
    IFinanceRepository repository,
    IFinanceRecognitionQueue? recognitionQueue = null,
    TimeProvider? timeProvider = null) : IFinanceService
{
    /// <summary>Сырой ответ модели храним для отладки, но не безгранично.</summary>
    public const int MaxRawResponseLength = 64 * 1024;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public bool RecognitionEnabled => recognitionQueue?.IsEnabled == true;

    /// <summary>Текущее время с точностью PostgreSQL (микросекунды): ответы POST/PUT совпадают с последующими GET.</summary>
    private DateTime UtcNow
    {
        get
        {
            var now = _time.GetUtcNow().UtcDateTime;
            return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        }
    }

    public async Task<FinanceReceipt> UploadReceiptAsync(string userId, byte[] data, string contentType, DateOnly purchasedOnFallback)
    {
        var now = UtcNow;
        var enabled = RecognitionEnabled;
        var receipt = new FinanceReceipt
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Source = FinanceReceiptSource.PHOTO,
            Status = enabled ? FinanceReceiptStatus.PENDING : FinanceReceiptStatus.FAILED,
            FailureReason = enabled ? null : FinanceFailureReason.DISABLED,
            PurchasedOn = purchasedOnFallback,
            UploadedOn = purchasedOnFallback,
            Total = 0,
            Currency = FinanceDefaults.Currency,
            Category = FinanceCategory.OTHER,
            HasImage = true,
            Revision = await repository.NextRevisionAsync(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await repository.AddReceiptAsync(receipt, new FinanceReceiptImage
        {
            ReceiptId = receipt.Id,
            Data = data,
            ContentType = contentType
        });

        if (enabled)
            recognitionQueue!.Enqueue(receipt.Id);

        return receipt;
    }

    public async Task<FinanceReceipt> CreateManualReceiptAsync(string userId, FinanceReceiptUpdate input)
    {
        var now = UtcNow;
        var receipt = new FinanceReceipt
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Source = FinanceReceiptSource.MANUAL,
            Status = FinanceReceiptStatus.READY,
            PurchasedOn = input.PurchasedOn,
            UploadedOn = input.PurchasedOn,
            Store = NullIfEmpty(input.Store),
            Total = Math.Round(input.Total, 2, MidpointRounding.AwayFromZero),
            Currency = input.Currency,
            Category = input.Category,
            Note = NullIfEmpty(input.Note),
            HasImage = false,
            Revision = await repository.NextRevisionAsync(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await repository.AddReceiptAsync(receipt, null);
        return receipt;
    }

    public async Task<List<FinanceReceiptListEntry>> GetReceiptsAsync(string userId, FinanceMonth month, FinanceCategory? category)
    {
        var receipts = await repository.GetReceiptsAsync(userId, month.From, month.ToExclusive);
        var result = new List<FinanceReceiptListEntry>();

        foreach (var receipt in receipts
                     .OrderByDescending(x => x.PurchasedOn)
                     .ThenByDescending(x => x.CreatedAt)
                     .ThenBy(x => x.Id))
        {
            var allocation = receipt.Status == FinanceReceiptStatus.READY
                ? FinanceAllocation.Allocate(receipt)
                : [];

            decimal? categoryAmount = null;
            if (category.HasValue)
            {
                // Как в сводке: чеки в другой валюте в категории не входят — иначе суммы списка не сойдутся с ней.
                if (!IsBaseCurrency(receipt))
                    continue;

                if (!allocation.TryGetValue(category.Value, out var amount) || amount <= 0)
                    continue;
                categoryAmount = amount;
            }

            result.Add(new FinanceReceiptListEntry
            {
                Receipt = receipt,
                CategoryAmount = categoryAmount,
                TopCategories = allocation
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key)
                    .Take(3)
                    .Select(x => x.Key)
                    .ToList()
            });
        }

        return result;
    }

    public Task<FinanceReceipt?> GetReceiptAsync(string userId, string receiptId)
    {
        return repository.GetReceiptAsync(userId, receiptId);
    }

    public async Task<FinanceReceipt?> UpdateReceiptAsync(string userId, string receiptId, FinanceReceiptUpdate update)
    {
        var existing = await repository.GetReceiptAsync(userId, receiptId);
        if (existing is null)
            return null;

        var now = UtcNow;
        var previous = existing.Items.ToDictionary(x => x.Id);
        var learned = new Dictionary<string, FinanceCategory>();
        var items = new List<FinanceReceiptItem>();

        foreach (var input in update.Items)
        {
            var source = FinanceCategorySource.USER;
            if (input.Id is not null && previous.TryGetValue(input.Id, out var old))
            {
                if (old.Category == input.Category)
                {
                    source = old.CategorySource;
                }
                else if (FinanceItemKey.Of(input.Name) is { } key)
                {
                    // Пользователь поправил категорию — запоминаем на будущее (последняя правка выигрывает).
                    learned[key] = input.Category;
                }
            }

            items.Add(new FinanceReceiptItem
            {
                Id = Guid.NewGuid().ToString(),
                ReceiptId = receiptId,
                Position = items.Count,
                Name = input.Name.Trim(),
                Quantity = input.Quantity,
                Price = input.Price,
                Amount = Math.Round(input.Amount, 2, MidpointRounding.AwayFromZero),
                Category = input.Category,
                CategorySource = source
            });
        }

        update.Store = NullIfEmpty(update.Store);
        update.Note = NullIfEmpty(update.Note);
        update.Total = Math.Round(update.Total, 2, MidpointRounding.AwayFromZero);

        var revision = await repository.NextRevisionAsync();
        if (!await repository.ReplaceReceiptAsync(userId, receiptId, update, items, revision, now))
            return null;

        if (learned.Count > 0)
            await repository.UpsertRulesAsync(userId, learned, now);

        return await repository.GetReceiptAsync(userId, receiptId);
    }

    public Task<bool> DeleteReceiptAsync(string userId, string receiptId)
    {
        return repository.DeleteReceiptAsync(userId, receiptId);
    }

    public async Task<FinanceOperationResult<FinanceReceipt>> RecognizeAgainAsync(string userId, string receiptId)
    {
        var existing = await repository.GetReceiptAsync(userId, receiptId);
        if (existing is null)
            return FinanceOperationResult<FinanceReceipt>.Fail(FinanceOperationStatus.NotFound);
        if (!existing.HasImage)
            return FinanceOperationResult<FinanceReceipt>.Fail(FinanceOperationStatus.NoImage);
        if (!RecognitionEnabled)
            return FinanceOperationResult<FinanceReceipt>.Fail(FinanceOperationStatus.Disabled);

        var status = await repository.MarkPendingAsync(userId, receiptId, await repository.NextRevisionAsync(), UtcNow);
        if (status != FinanceOperationStatus.Ok)
            return FinanceOperationResult<FinanceReceipt>.Fail(status);

        recognitionQueue!.Enqueue(receiptId);
        var receipt = await repository.GetReceiptAsync(userId, receiptId);
        return receipt is null
            ? FinanceOperationResult<FinanceReceipt>.Fail(FinanceOperationStatus.NotFound)
            : FinanceOperationResult<FinanceReceipt>.Ok(receipt);
    }

    public Task<FinanceReceiptImage?> GetImageAsync(string userId, string receiptId)
    {
        return repository.GetImageAsync(userId, receiptId);
    }

    public async Task<FinanceMonthSummary> GetSummaryAsync(string userId, FinanceMonth month)
    {
        var previousMonth = month.Previous;
        var receipts = await repository.GetReceiptsAsync(userId, previousMonth.From, month.ToExclusive);
        var current = receipts.Where(x => x.PurchasedOn >= month.From).ToList();
        var previous = receipts.Where(x => x.PurchasedOn < month.From).ToList();

        var amounts = new Dictionary<FinanceCategory, decimal>();
        var counts = new Dictionary<FinanceCategory, int>();
        var summary = new FinanceMonthSummary { Month = month };

        foreach (var receipt in current)
        {
            switch (receipt.Status)
            {
                case FinanceReceiptStatus.PENDING:
                    summary.PendingCount++;
                    continue;
                case FinanceReceiptStatus.FAILED:
                    summary.FailedCount++;
                    continue;
            }

            if (!IsBaseCurrency(receipt))
            {
                summary.OtherCurrencyCount++;
                continue;
            }

            summary.ReceiptCount++;
            summary.Total += receipt.Total;
            foreach (var (category, amount) in FinanceAllocation.Allocate(receipt))
            {
                amounts[category] = amounts.GetValueOrDefault(category) + amount;
                counts[category] = counts.GetValueOrDefault(category) + 1;
            }
        }

        summary.PreviousTotal = previous
            .Where(x => x.Status == FinanceReceiptStatus.READY && IsBaseCurrency(x))
            .Sum(x => x.Total);
        summary.Categories = amounts
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => new FinanceCategoryAmount { Category = x.Key, Amount = x.Value, ReceiptCount = counts[x.Key] })
            .ToList();
        return summary;
    }

    public async Task<bool> CompleteRecognitionAsync(FinancePendingRecognition pending, string modelResponse)
    {
        var parsed = ReceiptRecognitionParser.Parse(modelResponse);
        var keys = parsed?.Items
            .Select(x => FinanceItemKey.Of(x.Name))
            .OfType<string>()
            .Distinct()
            .ToList() ?? [];
        var rules = keys.Count > 0 ? await repository.GetRulesAsync(pending.UserId, keys) : [];

        var (result, failure) = BuildRecognitionResult(parsed, pending.UploadedOn, rules);
        var raw = Truncate(modelResponse);
        if (result is null)
            return await repository.FailRecognitionAsync(pending.ReceiptId, pending.Revision, failure!.Value, raw, UtcNow);

        result.RawJson = raw;
        foreach (var item in result.Items)
            item.ReceiptId = pending.ReceiptId;

        return await repository.CompleteRecognitionAsync(pending.ReceiptId, pending.Revision, result, UtcNow);
    }

    public Task<bool> FailRecognitionAsync(FinancePendingRecognition pending, FinanceFailureReason reason, string? rawResponse = null)
    {
        return repository.FailRecognitionAsync(pending.ReceiptId, pending.Revision, reason, Truncate(rawResponse), UtcNow);
    }

    /// <summary>
    /// Превращает разобранный ответ модели в данные чека: правила пользователя важнее категорий модели,
    /// итог — из чека или сумма позиций, дата — из чека, если она правдоподобна.
    /// </summary>
    public static (FinanceRecognitionResult? Result, FinanceFailureReason? Failure) BuildRecognitionResult(
        RecognizedReceipt? parsed,
        DateOnly fallbackDate,
        IReadOnlyDictionary<string, FinanceCategory> rules)
    {
        if (parsed is null)
            return (null, FinanceFailureReason.UNREADABLE);
        if (!parsed.IsReceipt)
            return (null, FinanceFailureReason.NOT_A_RECEIPT);

        var items = parsed.Items
            .Select((x, index) =>
            {
                var key = FinanceItemKey.Of(x.Name);
                var ruled = key is not null && rules.TryGetValue(key, out var category);
                return new FinanceReceiptItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Position = index,
                    Name = x.Name,
                    Quantity = x.Quantity,
                    Price = x.Price,
                    Amount = x.Amount,
                    Category = ruled ? rules[key!] : x.Category,
                    CategorySource = ruled ? FinanceCategorySource.RULE : FinanceCategorySource.AI
                };
            })
            .ToList();

        var itemsSum = items.Sum(x => x.Amount);
        var total = parsed.Total ?? (itemsSum is > 0 and <= FinanceDefaults.MaxAmount ? itemsSum : null);
        if (total is null)
            return (null, FinanceFailureReason.UNREADABLE);

        var date = parsed.Date is { } recognized
                   && recognized <= fallbackDate.AddDays(1)
                   && recognized >= fallbackDate.AddYears(-3)
            ? recognized
            : fallbackDate;

        return (new FinanceRecognitionResult
        {
            PurchasedOn = date,
            Store = parsed.Store,
            Total = total.Value,
            Currency = parsed.Currency,
            Category = FinanceAllocation.DominantCategory(items.Select(x => (x.Category, x.Amount)))
                       ?? parsed.Category
                       ?? FinanceCategory.OTHER,
            Items = items
        }, null);
    }

    private static bool IsBaseCurrency(FinanceReceipt receipt)
    {
        return string.Equals(receipt.Currency, FinanceDefaults.Currency, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Truncate(string? value)
    {
        // PostgreSQL text не хранит NUL и одиночные суррогаты; обрезаем, не разрывая суррогатную пару.
        return value is null ? null : TextSafety.Truncate(TextSafety.Clean(value, keepLineBreaks: true), MaxRawResponseLength);
    }
}
