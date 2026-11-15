using Microsoft.AspNetCore.Identity;

namespace Application.Modules.Finance;

public class FinanceReceipt
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public FinanceReceiptSource Source { get; set; }
    public FinanceReceiptStatus Status { get; set; }
    public FinanceFailureReason? FailureReason { get; set; }

    /// <summary>Дата покупки (без времени и часового пояса — как напечатано на чеке).</summary>
    public DateOnly PurchasedOn { get; set; }

    public string? Store { get; set; }

    /// <summary>Итог чека — главная сумма; позиции распределяются пропорционально ему.</summary>
    public decimal Total { get; set; }

    public string Currency { get; set; } = FinanceDefaults.Currency;

    /// <summary>Категория чека целиком: используется, когда позиций нет.</summary>
    public FinanceCategory Category { get; set; } = FinanceCategory.OTHER;

    public string? Note { get; set; }
    public bool HasImage { get; set; }

    /// <summary>Номер версии чека: результат распознавания сохраняется, только если чек с тех пор не меняли.</summary>
    public int Revision { get; set; }

    /// <summary>Сырой ответ модели — для отладки и повторного разбора.</summary>
    public string? RecognitionJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
    public List<FinanceReceiptItem> Items { get; set; } = [];
}

public class FinanceReceiptItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ReceiptId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Сумма позиции; отрицательная — скидка.</summary>
    public decimal Amount { get; set; }

    public FinanceCategory Category { get; set; }
    public FinanceCategorySource CategorySource { get; set; }
    public FinanceReceipt Receipt { get; set; } = null!;
}

/// <summary>Фото чека хранится отдельно, чтобы списки чеков не тянули байты.</summary>
public class FinanceReceiptImage
{
    public string ReceiptId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public FinanceReceipt Receipt { get; set; } = null!;
}

/// <summary>Правило «позиция с таким ключом → категория», выученное на правке пользователя.</summary>
public class FinanceCategoryRule
{
    public string UserId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public FinanceCategory Category { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IdentityUser User { get; set; } = null!;
}

public static class FinanceDefaults
{
    public const string Currency = "RUB";
    public const int MaxItems = 150;
    public const int MaxStoreLength = 120;
    public const int MaxNoteLength = 500;
    public const int MaxItemNameLength = 200;
    public const decimal MaxAmount = 10_000_000m;
}
