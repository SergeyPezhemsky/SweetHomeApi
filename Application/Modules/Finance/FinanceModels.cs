using System.Globalization;

namespace Application.Modules.Finance;

/// <summary>Календарный месяц «yyyy-MM».</summary>
public readonly record struct FinanceMonth(int Year, int Month)
{
    public DateOnly From => new(Year, Month, 1);
    public DateOnly ToExclusive => From.AddMonths(1);
    public FinanceMonth Previous => FromDate(From.AddMonths(-1));

    public override string ToString() => $"{Year:D4}-{Month:D2}";

    public static FinanceMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    public static bool TryParse(string? value, out FinanceMonth month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(value)
            || !DateOnly.TryParseExact(value.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.Year < 2000 || date.Year > 2100)
            return false;

        month = FromDate(date);
        return true;
    }
}

/// <summary>Чек в списке месяца; CategoryAmount — сколько досталось выбранной категории (если фильтр задан).</summary>
public class FinanceReceiptListEntry
{
    public FinanceReceipt Receipt { get; set; } = null!;
    public decimal? CategoryAmount { get; set; }

    /// <summary>Основные категории чека по распределённой сумме (до трёх).</summary>
    public List<FinanceCategory> TopCategories { get; set; } = [];
}

public class FinanceCategoryAmount
{
    public FinanceCategory Category { get; set; }
    public decimal Amount { get; set; }
    public int ReceiptCount { get; set; }
}

public class FinanceMonthSummary
{
    public FinanceMonth Month { get; set; }
    public decimal Total { get; set; }
    public int ReceiptCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>Готовые чеки не в рублях: в итог не входят.</summary>
    public int OtherCurrencyCount { get; set; }

    public decimal PreviousTotal { get; set; }
    public List<FinanceCategoryAmount> Categories { get; set; } = [];
}

/// <summary>Правка чека пользователем: поля и полный новый список позиций.</summary>
public class FinanceReceiptUpdate
{
    public DateOnly PurchasedOn { get; set; }
    public string? Store { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = FinanceDefaults.Currency;
    public FinanceCategory Category { get; set; }
    public string? Note { get; set; }
    public List<FinanceReceiptItemUpdate> Items { get; set; } = [];
}

public class FinanceReceiptItemUpdate
{
    /// <summary>Id существующей позиции — чтобы понять, сменил ли пользователь категорию. Null — новая позиция.</summary>
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal Amount { get; set; }
    public FinanceCategory Category { get; set; }
}

/// <summary>Чек, ожидающий распознавания.</summary>
public record FinancePendingRecognition(
    string ReceiptId,
    string UserId,
    int Revision,
    DateOnly UploadedOn,
    DateOnly PurchasedOn,
    byte[] Data,
    string ContentType);

/// <summary>Результат распознавания, готовый к сохранению.</summary>
public class FinanceRecognitionResult
{
    public DateOnly PurchasedOn { get; set; }
    public string? Store { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = FinanceDefaults.Currency;
    public FinanceCategory Category { get; set; }
    public List<FinanceReceiptItem> Items { get; set; } = [];
}

public record FinanceRecognitionBatch(
    IReadOnlyList<FinanceRecognitionResult> Receipts, bool IsBankScreenshot, string? RawJson, string? Model)
{
    public FinanceReceipt CreateAdditionalReceipt(FinancePendingRecognition pending, FinanceRecognitionResult purchase,
        int revision, DateTime now) => new()
    {
        UserId = pending.UserId,
        Source = FinanceReceiptSource.BANK_SCREENSHOT,
        Status = FinanceReceiptStatus.READY,
        PurchasedOn = purchase.PurchasedOn,
        UploadedOn = pending.UploadedOn,
        Store = purchase.Store,
        Total = purchase.Total,
        Currency = purchase.Currency,
        Category = purchase.Category,
        RecognitionModel = Model,
        Revision = revision,
        CreatedAt = now,
        UpdatedAt = now
    };
}

public enum FinanceOperationStatus
{
    Ok,
    NotFound,

    /// <summary>У чека нет фото.</summary>
    NoImage,
    AlreadyImported,

    /// <summary>Распознавание выключено в настройках сервера.</summary>
    Disabled
}

public class FinanceOperationResult<T>
{
    public FinanceOperationStatus Status { get; init; }
    public T? Value { get; init; }

    public static FinanceOperationResult<T> Ok(T value) => new() { Status = FinanceOperationStatus.Ok, Value = value };
    public static FinanceOperationResult<T> Fail(FinanceOperationStatus status) => new() { Status = status };
}
