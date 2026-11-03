namespace Application.Modules.Wardrobe;

public enum WardrobeCategory
{
    TOP,
    BOTTOM,
    DRESS,
    OUTERWEAR,
    SHOES,
    ACCESSORY
}

public enum WardrobeSeason
{
    SPRING,
    SUMMER,
    AUTUMN,
    WINTER
}

/// <summary>Состояние обработки фото (удаление фона).</summary>
public enum WardrobeImageStatus
{
    NONE,
    PENDING,
    READY,
    FAILED
}

/// <summary>Какая версия фото показывается: оригинал или «чистая» без фона.</summary>
public enum WardrobeImageVariant
{
    ORIGINAL,
    PROCESSED
}

public enum WardrobeStyle
{
    CASUAL,
    OFFICE,
    FORMAL,
    SPORT,
    HOME
}
