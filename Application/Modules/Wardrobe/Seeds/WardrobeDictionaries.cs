namespace Application.Modules.Wardrobe.Seeds;

public record WardrobeDictionaryItem(string Code, string Name);

public record WardrobeColor(string Code, string Name, string Hex, bool Neutral);

public record WardrobeWarmthLevel(int Level, string Name);

public static class WardrobeDictionaries
{
    public static readonly IReadOnlyList<WardrobeDictionaryItem> Categories =
    [
        new(nameof(WardrobeCategory.TOP), "Верх"),
        new(nameof(WardrobeCategory.BOTTOM), "Низ"),
        new(nameof(WardrobeCategory.DRESS), "Платья и комбинезоны"),
        new(nameof(WardrobeCategory.OUTERWEAR), "Верхняя одежда"),
        new(nameof(WardrobeCategory.SHOES), "Обувь"),
        new(nameof(WardrobeCategory.ACCESSORY), "Аксессуары")
    ];

    public static readonly IReadOnlyList<WardrobeDictionaryItem> Seasons =
    [
        new(nameof(WardrobeSeason.SPRING), "Весна"),
        new(nameof(WardrobeSeason.SUMMER), "Лето"),
        new(nameof(WardrobeSeason.AUTUMN), "Осень"),
        new(nameof(WardrobeSeason.WINTER), "Зима")
    ];

    public static readonly IReadOnlyList<WardrobeDictionaryItem> Styles =
    [
        new(nameof(WardrobeStyle.CASUAL), "Повседневный"),
        new(nameof(WardrobeStyle.OFFICE), "Офис"),
        new(nameof(WardrobeStyle.FORMAL), "Торжественный"),
        new(nameof(WardrobeStyle.SPORT), "Спорт"),
        new(nameof(WardrobeStyle.HOME), "Дом")
    ];

    public static readonly IReadOnlyList<WardrobeColor> Colors =
    [
        new("BLACK", "Чёрный", "#1A1A1A", true),
        new("WHITE", "Белый", "#F5F5F2", true),
        new("GRAY", "Серый", "#8C8C8C", true),
        new("BEIGE", "Бежевый", "#D8C3A5", true),
        new("BROWN", "Коричневый", "#6B4423", true),
        new("NAVY", "Тёмно-синий", "#1F2A44", true),
        new("BLUE", "Синий", "#2F6FD6", false),
        new("LIGHT_BLUE", "Голубой", "#9CC7F0", false),
        new("RED", "Красный", "#C62828", false),
        new("BURGUNDY", "Бордовый", "#6D1A2B", false),
        new("PINK", "Розовый", "#F2A7C3", false),
        new("ORANGE", "Оранжевый", "#F08A24", false),
        new("YELLOW", "Жёлтый", "#F2D13D", false),
        new("GREEN", "Зелёный", "#3D8B4A", false),
        new("OLIVE", "Оливковый", "#6B6B2E", false),
        new("PURPLE", "Фиолетовый", "#7B4EA3", false),
        new("MULTI", "Принт / пёстрый", "#B07CC6", false)
    ];

    public static readonly IReadOnlyList<WardrobeWarmthLevel> WarmthLevels =
    [
        new(1, "Очень лёгкая"),
        new(2, "Лёгкая"),
        new(3, "Средняя"),
        new(4, "Тёплая"),
        new(5, "Очень тёплая")
    ];

    public static WardrobeColor? FindColor(string code) =>
        Colors.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownColor(string code) => FindColor(code) is not null;

    public static string CategoryName(WardrobeCategory category) =>
        Categories.First(x => x.Code == category.ToString()).Name;

    public static string StyleName(WardrobeStyle style) =>
        Styles.First(x => x.Code == style.ToString()).Name;

    public static string SeasonName(WardrobeSeason season) =>
        Seasons.First(x => x.Code == season.ToString()).Name;

    public static string ColorName(string code) =>
        FindColor(code)?.Name.ToLowerInvariant() ?? code;
}
