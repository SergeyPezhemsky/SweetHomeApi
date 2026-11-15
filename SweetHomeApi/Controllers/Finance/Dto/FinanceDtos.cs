using Application.Modules.Finance;

namespace SweetHomeApi.Controllers.Finance.Dto;

public class FinanceCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

public class FinanceDictionariesDto
{
    public List<FinanceCategoryDto> Categories { get; set; } = [];
    public bool Recognition { get; set; }
}

public class FinanceReceiptItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal Amount { get; set; }
    public FinanceCategory Category { get; set; }
    public FinanceCategorySource CategorySource { get; set; }
}

public class FinanceReceiptDto
{
    public string Id { get; set; } = string.Empty;
    public FinanceReceiptSource Source { get; set; }
    public FinanceReceiptStatus Status { get; set; }
    public FinanceFailureReason? FailureReason { get; set; }
    public string PurchasedOn { get; set; } = string.Empty;
    public string? Store { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
    public FinanceCategory Category { get; set; }
    public string? Note { get; set; }
    public bool HasImage { get; set; }
    public int ItemCount { get; set; }

    /// <summary>Сумма позиций (может отличаться от итога: скидки, округления, нераспознанные строки).</summary>
    public decimal ItemsTotal { get; set; }

    /// <summary>Только в списке с фильтром категории: сколько этой категории досталось от итога чека.</summary>
    public decimal? CategoryAmount { get; set; }

    /// <summary>Основные категории чека по распределённой сумме (до трёх).</summary>
    public List<FinanceCategory> TopCategories { get; set; } = [];

    /// <summary>Только в карточке чека.</summary>
    public List<FinanceReceiptItemDto>? Items { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FinanceReceiptListDto
{
    public string Month { get; set; } = string.Empty;
    public List<FinanceReceiptDto> Receipts { get; set; } = [];
}

public class FinanceCategoryAmountDto
{
    public FinanceCategory Category { get; set; }
    public decimal Amount { get; set; }
    public int ReceiptCount { get; set; }
}

public class FinanceSummaryDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int ReceiptCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public int OtherCurrencyCount { get; set; }
    public decimal PreviousTotal { get; set; }
    public List<FinanceCategoryAmountDto> Categories { get; set; } = [];
}

public class CreateManualReceiptDto
{
    public decimal? Total { get; set; }
    public string? Category { get; set; }
    public string? PurchasedOn { get; set; }
    public string? Store { get; set; }
    public string? Currency { get; set; }
    public string? Note { get; set; }
}

public class UpdateReceiptItemDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? Amount { get; set; }
    public string? Category { get; set; }
}

public class UpdateReceiptDto
{
    public string? PurchasedOn { get; set; }
    public string? Store { get; set; }
    public decimal? Total { get; set; }
    public string? Currency { get; set; }
    public string? Category { get; set; }
    public string? Note { get; set; }
    public List<UpdateReceiptItemDto>? Items { get; set; }
}
