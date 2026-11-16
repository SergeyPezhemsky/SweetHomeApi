namespace Application.Modules.Finance;

public interface IFinanceRepository
{
    /// <summary>Следующий номер ревизии чека (общая последовательность).</summary>
    Task<int> NextRevisionAsync();

    /// <summary>Сохраняет новый чек вместе с позициями и фото (если есть).</summary>
    Task AddReceiptAsync(FinanceReceipt receipt, FinanceReceiptImage? image);

    /// <summary>Чеки пользователя за период [from, toExclusive) с позициями, без фото.</summary>
    Task<List<FinanceReceipt>> GetReceiptsAsync(string userId, DateOnly from, DateOnly toExclusive);

    /// <summary>Чек с позициями (по порядку), без отслеживания.</summary>
    Task<FinanceReceipt?> GetReceiptAsync(string userId, string receiptId);

    Task<FinanceReceiptImage?> GetImageAsync(string userId, string receiptId);

    /// <summary>
    /// Заменяет поля и позиции чека, ставит READY и новую ревизию (идущее распознавание будет отброшено).
    /// False — чека уже нет.
    /// </summary>
    Task<bool> ReplaceReceiptAsync(string userId, string receiptId, FinanceReceiptUpdate update, List<FinanceReceiptItem> items,
        int revision, DateTime updatedAt);

    Task<bool> DeleteReceiptAsync(string userId, string receiptId);

    /// <summary>Ставит чек с фото в очередь с новой ревизией. NotFound / NoImage / Ok.</summary>
    Task<FinanceOperationStatus> MarkPendingAsync(string userId, string receiptId, int revision, DateTime updatedAt);

    Task<List<string>> GetPendingReceiptIdsAsync();
    Task<FinancePendingRecognition?> GetPendingRecognitionAsync(string receiptId);

    /// <summary>Сохраняет весь результат одной транзакцией, если исходный чек всё ещё PENDING с той же ревизией.</summary>
    Task<bool> CompleteRecognitionAsync(FinancePendingRecognition pending, FinanceRecognitionBatch batch, DateTime updatedAt);

    /// <summary>Помечает FAILED, если чек всё ещё PENDING с той же ревизией.</summary>
    Task<bool> FailRecognitionAsync(string receiptId, int revision, FinanceFailureReason reason, string? rawJson, string? model,
        DateTime updatedAt);

    Task<Dictionary<string, FinanceCategory>> GetRulesAsync(string userId, IReadOnlyCollection<string> keys);

    /// <summary>Создаёт или обновляет правила «ключ → категория» (без потерянных обновлений при параллельных правках).</summary>
    Task UpsertRulesAsync(string userId, IReadOnlyDictionary<string, FinanceCategory> rules, DateTime updatedAt);
}
