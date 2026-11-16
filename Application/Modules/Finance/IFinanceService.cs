namespace Application.Modules.Finance;

public interface IFinanceService
{
    bool RecognitionEnabled { get; }

    /// <summary>Создаёт чек из фото: PENDING и в очередь, либо FAILED/DISABLED, если распознавание выключено.</summary>
    Task<FinanceReceipt> UploadReceiptAsync(string userId, byte[] data, string contentType, DateOnly purchasedOnFallback);

    Task<FinanceReceipt> CreateManualReceiptAsync(string userId, FinanceReceiptUpdate input);

    Task<List<FinanceReceiptListEntry>> GetReceiptsAsync(string userId, FinanceMonth month, FinanceCategory? category);
    Task<FinanceReceipt?> GetReceiptAsync(string userId, string receiptId);
    Task<FinanceReceipt?> UpdateReceiptAsync(string userId, string receiptId, FinanceReceiptUpdate update);
    Task<bool> DeleteReceiptAsync(string userId, string receiptId);
    Task<FinanceOperationResult<FinanceReceipt>> RecognizeAgainAsync(string userId, string receiptId);
    Task<FinanceReceiptImage?> GetImageAsync(string userId, string receiptId);
    Task<FinanceMonthSummary> GetSummaryAsync(string userId, FinanceMonth month);

    /// <summary>Разбирает ответ модели (с правилами пользователя), ничего не сохраняя: воркер решает, звать ли следующего провайдера.</summary>
    Task<(FinanceRecognitionBatch? Result, FinanceFailureReason? Failure)> EvaluateRecognitionAsync(
        FinancePendingRecognition pending, string modelResponse, string? model = null);

    /// <summary>Сохраняет результат для этой ревизии. False — чек изменился или удалён.</summary>
    Task<bool> SaveRecognitionAsync(FinancePendingRecognition pending, FinanceRecognitionBatch result);

    /// <summary>Разбирает ответ модели и сохраняет результат (или причину неудачи) для этой ревизии.</summary>
    Task<bool> CompleteRecognitionAsync(FinancePendingRecognition pending, string modelResponse, string? model = null);

    Task<bool> FailRecognitionAsync(FinancePendingRecognition pending, FinanceFailureReason reason, string? rawResponse = null,
        string? model = null);
}
