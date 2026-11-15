namespace Application.Modules.Finance;

public enum FinanceCategory
{
    GROCERIES,
    CAFE,
    TRANSPORT,
    CAR,
    HOME,
    HEALTH,
    BEAUTY,
    CLOTHES,
    PETS,
    ELECTRONICS,
    ENTERTAINMENT,
    SERVICES,
    UTILITIES,
    TRAVEL,
    GIFTS,
    EDUCATION,
    OTHER
}

public enum FinanceReceiptSource
{
    PHOTO,
    MANUAL
}

public enum FinanceReceiptStatus
{
    /// <summary>Фото в очереди на распознавание.</summary>
    PENDING,
    READY,
    FAILED
}

public enum FinanceFailureReason
{
    /// <summary>На фото не чек.</summary>
    NOT_A_RECEIPT,

    /// <summary>Модель ответила, но разобрать ответ или найти сумму не удалось.</summary>
    UNREADABLE,

    /// <summary>Сервис распознавания недоступен или вернул ошибку.</summary>
    PROVIDER_ERROR,

    /// <summary>Распознавание выключено в настройках сервера.</summary>
    DISABLED,

    /// <summary>Ответ получен, но сохранить его не удалось (ошибка на сервере).</summary>
    INTERNAL_ERROR
}

/// <summary>Откуда у позиции категория.</summary>
public enum FinanceCategorySource
{
    AI,

    /// <summary>Из правила, выученного на правках пользователя.</summary>
    RULE,

    USER
}
