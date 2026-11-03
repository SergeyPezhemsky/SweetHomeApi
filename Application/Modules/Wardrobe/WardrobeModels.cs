namespace Application.Modules.Wardrobe;

public class WardrobeItemQuery
{
    public string? Query { get; set; }
    public WardrobeCategory? Category { get; set; }
    public string? Season { get; set; }
    public string? Style { get; set; }
    public string? Color { get; set; }
    public bool? Favorite { get; set; }
}

public class WardrobeItemListResult
{
    public List<WardrobeItem> Items { get; set; } = [];
    public int Total { get; set; }

    /// <summary>Количество вещей по категориям во всём гардеробе пользователя (без учёта фильтров).</summary>
    public Dictionary<WardrobeCategory, int> Counts { get; set; } = [];
}

public class WardrobeOutfitDetails
{
    public WardrobeOutfit Outfit { get; set; } = null!;
    public List<WardrobeItem> Items { get; set; } = [];
}

public enum WardrobeOperationStatus
{
    Ok,
    NotFound,
    InvalidItems,
    /// <summary>Действие сейчас невозможно (например, выбрать фото без фона, пока оно не готово).</summary>
    NotReady,
    /// <summary>У вещи нет фото.</summary>
    NoImage,
    /// <summary>Удаление фона выключено в настройках сервера.</summary>
    Disabled
}

public class WardrobeOperationResult<T>
{
    public WardrobeOperationStatus Status { get; init; }
    public T? Value { get; init; }

    public static WardrobeOperationResult<T> Ok(T value) => new() { Status = WardrobeOperationStatus.Ok, Value = value };
    public static WardrobeOperationResult<T> NotFound() => new() { Status = WardrobeOperationStatus.NotFound };
    public static WardrobeOperationResult<T> InvalidItems() => new() { Status = WardrobeOperationStatus.InvalidItems };
}
