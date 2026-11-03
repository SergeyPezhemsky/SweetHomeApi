using System.Globalization;
using System.Text;
using Application.Modules.Wardrobe.Seeds;

namespace Application.Modules.Wardrobe;

public class WardrobeClassification
{
    public WardrobeCategory? Category { get; set; }
    public List<string> Seasons { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public int? Warmth { get; set; }
    public List<string> Colors { get; set; } = [];
    public double Confidence { get; set; }
    public List<string> MatchedKeywords { get; set; } = [];
}

/// <summary>
/// Классификатор вещей по названию/описанию (русские и английские ключевые слова)
/// и по доминирующему цвету фотографии.
/// </summary>
public static class WardrobeClassifier
{
    private const string Spring = nameof(WardrobeSeason.SPRING);
    private const string Summer = nameof(WardrobeSeason.SUMMER);
    private const string Autumn = nameof(WardrobeSeason.AUTUMN);
    private const string Winter = nameof(WardrobeSeason.WINTER);

    private const string Casual = nameof(WardrobeStyle.CASUAL);
    private const string Office = nameof(WardrobeStyle.OFFICE);
    private const string Formal = nameof(WardrobeStyle.FORMAL);
    private const string Sport = nameof(WardrobeStyle.SPORT);
    private const string Home = nameof(WardrobeStyle.HOME);

    private sealed record CategoryRule(
        string[] Stems,
        WardrobeCategory Category,
        int Warmth,
        string[] Seasons,
        string[] Styles,
        bool ExactOnly = false);

    private sealed record ModifierRule(
        string[] Stems,
        int WarmthDelta = 0,
        string[]? Seasons = null,
        string[]? Styles = null,
        bool ExactOnly = false,
        int? OuterwearCap = null,
        bool SkipForAccessories = false);

    private sealed record ColorRule(string[] Stems, string Color, string[]? Excludes = null, bool ExactOnly = false);

    private static readonly string[] AllSeasons = [];
    private static readonly string[] Cold = [Autumn, Winter];
    private static readonly string[] Warm = [Spring, Summer];
    private static readonly string[] Demi = [Spring, Autumn];

    private static readonly CategoryRule[] CategoryRules =
    [
        // Верх
        new(["футболк", "футболоч", "t-shirt", "tshirt", "tee"], WardrobeCategory.TOP, 1, Warm, [Casual, Sport, Home]),
        new(["майка", "майки", "майку", "маек", "маечка", "маечки", "tank"], WardrobeCategory.TOP, 1, [Summer], [Casual, Sport, Home], ExactOnly: true),
        new(["топ", "топы", "топик", "топики", "top"], WardrobeCategory.TOP, 1, [Summer], [Casual], ExactOnly: true),
        new(["рубаш", "сорочк", "shirt"], WardrobeCategory.TOP, 2, AllSeasons, [Office, Casual, Formal]),
        new(["блуз", "blouse"], WardrobeCategory.TOP, 2, AllSeasons, [Office, Casual, Formal]),
        new(["поло", "polo"], WardrobeCategory.TOP, 2, Warm, [Casual, Sport], ExactOnly: true),
        new(["лонгслив", "longsleeve", "тельняшк", "боди", "bodysuit"], WardrobeCategory.TOP, 2, AllSeasons, [Casual, Home]),
        new(["водолазк", "бадлон", "turtleneck"], WardrobeCategory.TOP, 3, Cold, [Casual, Office]),
        new(["свитер", "sweater"], WardrobeCategory.TOP, 4, Cold, [Casual]),
        new(["гольф"], WardrobeCategory.TOP, 3, Cold, [Casual, Office], ExactOnly: true),
        new(["пижам", "pajama", "домашний костюм"], WardrobeCategory.TOP, 2, AllSeasons, [Home]),
        new(["джемпер", "jumper", "пуловер", "pullover"], WardrobeCategory.TOP, 3, [Spring, Autumn, Winter], [Casual, Office]),
        new(["кофт", "кардиган", "cardigan"], WardrobeCategory.TOP, 3, [Spring, Autumn, Winter], [Casual, Office, Home]),
        new(["худи", "hoodie", "толстовк", "свитшот", "sweatshirt", "олимпийк"], WardrobeCategory.TOP, 3, [Spring, Autumn, Winter], [Casual, Sport, Home]),

        // Низ
        new(["джинсы", "джинс", "джинсики", "jeans"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual], ExactOnly: true),
        new(["брюк", "брючк", "палаццо", "кюлот", "trousers", "slacks", "culottes", "dress pant"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Office, Casual, Formal]),
        new(["штан", "pants", "карго", "cargo", "чинос", "chinos"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual]),
        new(["джоггер", "joggers", "jogger", "треник", "sweatpants", "trackpants", "tracksuit", "спортивный костюм"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual, Sport, Home]),
        new(["шорт", "бридж", "shorts", "capri"], WardrobeCategory.BOTTOM, 1, [Summer], [Casual, Sport, Home]),
        new(["юбк", "юбочк", "skirt"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual, Office]),
        new(["легинс", "леггинс", "лосин", "leggings", "тайтс"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Sport, Home, Casual]),

        // Платья
        new(["плать", "dress"], WardrobeCategory.DRESS, 2, AllSeasons, [Casual, Office, Formal]),
        new(["платье-свитер", "платье свитер", "sweater dress", "knit dress"], WardrobeCategory.DRESS, 3, Cold, [Casual, Office]),
        new(["сарафан", "sundress"], WardrobeCategory.DRESS, 1, [Summer], [Casual]),
        new(["комбинезон", "jumpsuit"], WardrobeCategory.DRESS, 2, AllSeasons, [Casual]),

        // Верхняя одежда
        new(["пуховик", "пуховичок", "дубленоч", "puffer", "down jacket", "puffer jacket", "дубленк", "шуб", "parka", "парка"], WardrobeCategory.OUTERWEAR, 5, [Winter], [Casual]),
        new(["пальто", "пальтишк", "coat", "overcoat", "peacoat", "topcoat", "полупальто"], WardrobeCategory.OUTERWEAR, 4, [Spring, Autumn, Winter], [Casual, Office, Formal]),
        new(["куртк", "курточ", "джинсовк", "jacket", "анорак", "anorak"], WardrobeCategory.OUTERWEAR, 4, [Spring, Autumn, Winter], [Casual]),
        new(["плащ", "тренч", "trench", "trench coat", "raincoat"], WardrobeCategory.OUTERWEAR, 3, Demi, [Casual, Office]),
        new(["бомбер", "bomber", "косух", "кожанк", "ветровк", "windbreaker"], WardrobeCategory.OUTERWEAR, 3, Demi, [Casual, Sport]),
        new(["пиджак", "пиджач", "жакет", "блейзер", "blazer", "suit jacket", "sport coat"], WardrobeCategory.OUTERWEAR, 2, [Spring, Summer, Autumn], [Office, Formal]),
        new(["жилет", "безрукавк", "vest"], WardrobeCategory.OUTERWEAR, 2, Demi, [Casual]),

        // Обувь
        new(["кроссовк", "кроссовоч", "кроссы", "слипон", "sneaker", "trainer", "runner", "slip-on", "shoe"], WardrobeCategory.SHOES, 2, [Spring, Summer, Autumn], [Casual, Sport]),
        new(["кеды", "кед", "кедики", "converse"], WardrobeCategory.SHOES, 2, Warm, [Casual], ExactOnly: true),
        new(["ботин", "полусапож", "ботильон", "boot", "челси", "chelsea", "берц", "тимберленд", "мартинс", "martens", "казаки"], WardrobeCategory.SHOES, 4, [Spring, Autumn, Winter], [Casual, Office]),
        new(["сапог", "сапож", "ботфорт", "валенк", "угги", "ugg", "дутик"], WardrobeCategory.SHOES, 5, [Winter], [Casual, Office]),
        new(["туфл", "туфельк", "лодочк", "шпильк", "heels", "heel", "pumps", "oxford", "оксфорд", "дерби", "derby", "броги", "brogue", "dress shoe"], WardrobeCategory.SHOES, 2, [Spring, Summer, Autumn], [Office, Formal]),
        new(["лофер", "loafer", "мокасин", "moccasin", "балетк", "ballet", "мюли", "сабо", "mules", "clogs"], WardrobeCategory.SHOES, 2, [Spring, Summer, Autumn], [Casual, Office]),
        new(["босонож", "сандал", "sandal", "шлеп", "сланц", "flip-flop", "flip flop", "вьетнамк", "эспадрил", "espadrille"], WardrobeCategory.SHOES, 1, [Summer], [Casual]),
        new(["тапк", "тапоч", "кроксы", "crocs", "slipper"], WardrobeCategory.SHOES, 1, AllSeasons, [Home]),

        // Аксессуары
        new(["шапк", "шапоч", "beanie", "ушанк"], WardrobeCategory.ACCESSORY, 4, [Winter], [Casual]),
        new(["шарф", "снуд", "палантин", "scarf"], WardrobeCategory.ACCESSORY, 4, Cold, [Casual, Office]),
        new(["перчат", "варежк", "gloves", "mittens"], WardrobeCategory.ACCESSORY, 4, [Winter], [Casual, Office]),
        new(["кепк", "кепоч", "бейсболк", "панам", "cap", "панама"], WardrobeCategory.ACCESSORY, 1, [Summer], [Casual, Sport]),
        new(["шляп", "hat", "берет", "beret"], WardrobeCategory.ACCESSORY, 2, AllSeasons, [Casual]),
        new(["галстук", "tie", "бабочк", "bowtie"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Office, Formal]),
        new(["ремень", "ремн", "пояс", "belt"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Casual, Office, Formal]),
        new(["сумк", "сумоч", "рюкзак", "рюкзач", "клатч", "bag", "handbag", "tote", "backpack", "clutch"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Casual, Office]),
        new(["очки", "sunglasses", "часы", "watch", "платок", "серьг", "сереж", "колье", "браслет", "earring", "necklace", "bracelet"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Casual, Office, Formal])
    ];

    private static readonly ModifierRule[] ModifierRules =
    [
        new(["тепл", "утепл", "warm", "insulated", "thermal", "термо", "padded"], WarmthDelta: 1, Seasons: Cold),
        new(["шерст", "wool", "woolen", "woollen", "кашемир", "cashmere", "мохер", "ангор", "вязан", "knit", "knitted"], WarmthDelta: 1, Seasons: Cold),
        // Флис, деним и кожа греют меньше пуховика: такая верхняя одежда — межсезонная.
        new(["флис", "fleece"], WarmthDelta: 1, Seasons: Cold, OuterwearCap: 3),
        new(["джинсов", "denim", "деним", "кожан", "кожзам", "leather", "замш", "suede"], OuterwearCap: 3),
        // Резиновые сапоги и дождевики — от дождя, а не от мороза.
        new(["резинов", "rain", "непромока"], OuterwearCap: 3),
        new(["демисезон"], Seasons: Demi, OuterwearCap: 4),
        new(["зимн", "winter"], WarmthDelta: 1, Seasons: [Winter]),
        new(["пухов"], WarmthDelta: 1, Seasons: [Winter]),
        new(["летн", "summer"], WarmthDelta: -1, Seasons: [Summer]),
        new(["легк", "тонк", "light", "thin"], WarmthDelta: -1),
        new(["льнян", "linen", "шелк", "silk", "сетк"], WarmthDelta: -1, Seasons: Warm, SkipForAccessories: true),
        new(["лен"], WarmthDelta: -1, Seasons: Warm, ExactOnly: true, SkipForAccessories: true),
        new(["карго", "cargo"], Styles: [Casual]),
        new(["осенн", "autumn", "fall"], Seasons: [Autumn]),
        new(["весенн", "spring"], Seasons: [Spring]),
        new(["спорт", "sport", "sports", "athletic", "трениров", "беговы", "running", "фитнес", "gym", "йог", "yoga"], Styles: [Sport]),
        new(["офис", "делов", "office", "business", "классич", "classic"], Styles: [Office]),
        new(["вечерн", "праздн", "коктейл", "evening", "formal", "party", "нарядн"], Styles: [Formal]),
        new(["домашн", "home", "пижам", "pajama", "lounge"], Styles: [Home])
    ];

    private static readonly ColorRule[] ColorRules =
    [
        new(["темносин", "navy", "нэви", "индиго"], "NAVY"),
        new(["черн", "black"], "BLACK"),
        new(["бел", "white", "молочн", "кремов", "ivory", "cream"], "WHITE", Excludes: ["бель", "белорус"]),
        new(["сер", "gray", "grey", "charcoal", "графит", "антрацит"], "GRAY", Excludes: ["серьг", "сереж", "серд", "серебр", "серия", "серии"]),
        new(["серебр", "silver"], "GRAY"),
        new(["беж", "beige", "khaki", "песочн", "камел", "кэмел", "camel", "tan", "тауп", "taupe", "sand"], "BEIGE"),
        new(["коричн", "brown", "шоколад", "кофейн", "терракот"], "BROWN"),
        new(["голуб", "lightblue", "бирюз", "turquoise", "aqua"], "LIGHT_BLUE"),
        new(["син", "blue", "джинсов", "джинсик", "denim", "деним"], "BLUE", Excludes: ["синтет", "синтеп"]),
        new(["бордов", "бордо", "винн", "burgundy", "maroon", "wine", "марсал"], "BURGUNDY"),
        new(["красн", "red", "алый"], "RED"),
        new(["розов", "pink", "пудров", "фукси", "малинов", "персиков", "peach"], "PINK"),
        new(["оранж", "orange", "морков", "коралл", "coral", "rust", "ржав"], "ORANGE"),
        new(["желт", "yellow", "горчичн", "mustard", "лимонн", "золотист", "gold", "golden"], "YELLOW"),
        new(["оливк", "olive", "хаки", "милитари"], "OLIVE"),
        new(["зелен", "green", "изумруд", "мятн", "mint", "teal", "салатов"], "GREEN"),
        new(["фиолет", "purple", "violet", "сиренев", "лилов", "lilac", "лаванд", "lavender"], "PURPLE"),
        new(["принт", "print", "printed", "цветочн", "цветоч", "floral", "леопард", "leopard", "клетк", "клетчат", "plaid", "checked", "полоск", "striped", "stripe", "пестр", "разноцвет", "multicolor", "multicolored", "камуфляж", "camo", "tiedye", "горош", "polkadot"], "MULTI")
    ];

    private static readonly (string From, string To)[] CompoundReplacements =
    [
        ("темно-син", "темносин"),
        ("темно син", "темносин"),
        ("dark blue", "navy"),
        ("navy blue", "navy"),
        ("baby blue", "lightblue"),
        ("off-white", "offwhite"),
        ("off white", "offwhite"),
        ("black star", "blackstar"),
        ("red wing", "redwing"),
        ("light blue", "lightblue"),
        ("light-blue", "lightblue"),
        ("sky blue", "lightblue"),
        ("light grey", "grey"),
        ("light gray", "gray"),
        ("dark grey", "grey"),
        ("dark gray", "gray"),
        ("light pink", "pink"),
        ("light green", "green"),
        ("tie-dye", "tiedye"),
        ("tie dye", "tiedye"),
        ("polka dot", "polkadot"),
        ("polka-dot", "polkadot")
    ];

    /// <summary>
    /// Дополнительные опорные оттенки для цвета с фото: приглушённые и «джинсовые» синие,
    /// пастельные и тёмные варианты — палитра словаря слишком редкая для фотографий.
    /// </summary>
    private static readonly (string Code, string Hex)[] ColorAnchors =
    [
        ("BLUE", "#5D7B9D"), ("BLUE", "#4F6D8F"), ("BLUE", "#3E5F8A"), ("BLUE", "#4A6FA5"), ("BLUE", "#6F8FAF"), ("BLUE", "#1F4E99"),
        ("NAVY", "#202A3F"), ("NAVY", "#2C3E5C"),
        ("LIGHT_BLUE", "#87CEEB"), ("LIGHT_BLUE", "#B0C4DE"), ("LIGHT_BLUE", "#40E0D0"),
        ("PURPLE", "#800080"), ("PURPLE", "#4B0082"), ("PURPLE", "#B57EDC"), ("PURPLE", "#967BB6"), ("PURPLE", "#E6E6FA"),
        ("GREEN", "#98FF98"), ("GREEN", "#A8E4A0"), ("GREEN", "#2E8B57"), ("GREEN", "#50C878"), ("GREEN", "#1E5631"),
        ("OLIVE", "#808000"), ("OLIVE", "#556B2F"), ("OLIVE", "#BDB76B"),
        ("YELLOW", "#E1AD01"), ("YELLOW", "#FFDB58"), ("YELLOW", "#FFF44F"),
        ("ORANGE", "#FF7F50"), ("ORANGE", "#CC5500"),
        ("PINK", "#D8A0A6"), ("PINK", "#FFC0CB"), ("PINK", "#FF69B4"), ("PINK", "#C71585"),
        ("RED", "#DC143C"), ("RED", "#B22222"),
        ("BURGUNDY", "#800020"), ("BURGUNDY", "#5C0A1E"),
        ("BEIGE", "#C19A6B"), ("BEIGE", "#E1C699"), ("BEIGE", "#F5F5DC"),
        ("BROWN", "#8B5A2B"), ("BROWN", "#5C4033"), ("BROWN", "#A0522D"),
        ("GRAY", "#36454F"), ("GRAY", "#708090"), ("GRAY", "#A9A9A9"), ("GRAY", "#D3D3D3"),
        ("BEIGE", "#C3B091"), ("BEIGE", "#C2B280"), ("OLIVE", "#4B5320"), ("GREEN", "#9CAF88"),
        ("NAVY", "#141B2D"), ("NAVY", "#262D3D"), ("BROWN", "#3E2723"), ("BEIGE", "#ADA587"), ("OLIVE", "#8A865D"),
        ("PURPLE", "#C8A2C8"), ("BEIGE", "#E3D9C6"),
        ("PURPLE", "#D8BFD8"), ("PURPLE", "#614051"), ("YELLOW", "#FDFD96")
    ];

    private static readonly string[] AdjectiveEndings =
        ["ый", "ий", "ой", "ая", "яя", "ое", "ее", "ые", "ие", "ого", "его", "ому", "ему", "ую", "юю", "ых", "их", "ым", "им"];

    public static WardrobeClassification Classify(string? name, string? notes = null, string? colorHex = null)
    {
        // Название главнее заметок: «White sneakers» с заметкой «к белой футболке» — это обувь, а не футболка.
        // Заметки в классификации не участвуют: в них обычно пишут про другие вещи
        // («ношу под тёплый свитер», «к белой футболке»), и они искажали бы категорию, цвет и теплоту.
        var nameText = Normalize(name ?? string.Empty);
        var nameTokens = Tokenize(nameText);
        var text = nameText;
        var tokens = nameTokens;
        var result = new WardrobeClassification();
        var matched = new List<string>();

        var categoryRule = FindCategoryRule(nameTokens, nameText, matched);
        var seasons = new HashSet<string>(categoryRule?.Seasons ?? []);
        var styles = new HashSet<string>(categoryRule?.Styles ?? []);
        var explicitSeasons = new HashSet<string>();
        var explicitStyles = new HashSet<string>();
        var warmth = categoryRule?.Warmth ?? 0;
        var signals = 0;

        int? outerwearCap = null;
        foreach (var modifier in ModifierRules)
        {
            if (modifier.SkipForAccessories && categoryRule?.Category == WardrobeCategory.ACCESSORY)
                continue;

            var token = FindMatch(tokens, text, modifier.Stems, modifier.ExactOnly, null);
            if (token is null)
                continue;

            matched.Add(token);
            signals++;
            warmth += modifier.WarmthDelta;
            if (modifier.OuterwearCap is { } cap)
                outerwearCap = Math.Min(outerwearCap ?? cap, cap);
            foreach (var season in modifier.Seasons ?? [])
                explicitSeasons.Add(season);
            foreach (var style in modifier.Styles ?? [])
                explicitStyles.Add(style);
        }

        if (explicitSeasons.Count > 0)
            seasons = explicitSeasons;

        // Джинсовка, кожанка, флисовая куртка: не теплее «средней», носятся в межсезонье.
        if (categoryRule?.Category is WardrobeCategory.OUTERWEAR or WardrobeCategory.SHOES && outerwearCap is { } outerCap)
        {
            warmth = Math.Min(warmth, outerCap);
            seasons = [..Demi];
        }

        // «Домашние штаны», «домашняя футболка» — только для дома, не для улицы.
        if (explicitStyles.SetEquals([Home]))
        {
            styles = [Home];
        }
        else if (explicitStyles.Count > 0)
        {
            // Явно названный стиль ставим первым, остальные стили категории оставляем как совместимые.
            styles = [..explicitStyles, ..styles.Where(x => !explicitStyles.Contains(x) && x != Home)];
        }

        // Цвет берём только из названия: в заметках обычно цвета других вещей («к белой рубашке»).
        var colors = new List<string>();
        foreach (var rule in ColorRules)
        {
            var token = FindMatch(nameTokens, nameText, rule.Stems, rule.ExactOnly, rule.Excludes);
            if (token is null || colors.Contains(rule.Color))
                continue;

            // «джинсовый» означает синий только если других цветов нет.
            if (rule.Color == "BLUE" && token.StartsWith("джинсов") && colors.Count > 0)
                continue;

            colors.Add(rule.Color);
            matched.Add(token);
        }

        // «джинсы» сами по себе обычно синие.
        if (colors.Count == 0 && categoryRule?.Stems.Contains("джинсы") == true)
            colors.Add("BLUE");

        if (colors.Count == 0 && TryParseHex(colorHex, out var rgb))
        {
            colors.Add(NearestColor(rgb));
            signals++;
        }

        if (categoryRule is null)
        {
            result.Colors = colors.Take(3).ToList();
            result.MatchedKeywords = matched.Distinct().ToList();
            result.Seasons = explicitSeasons.ToList();
            result.Styles = explicitStyles.ToList();
            result.Confidence = 0;
            return result;
        }

        if (colors.Count > 0)
            signals++;

        result.Category = categoryRule.Category;
        result.Warmth = Math.Clamp(warmth, 1, 5);
        result.Seasons = OrderSeasons(seasons);
        result.Styles = styles.ToList();
        result.Colors = colors.Take(3).ToList();
        result.MatchedKeywords = matched.Distinct().ToList();
        result.Confidence = Math.Round(Math.Min(0.95, 0.6 + 0.1 * signals), 2);
        return result;
    }

    /// <summary>«Место» аксессуара: в образе не больше одного на место (не две шапки, не шарф и палантин).</summary>
    public static string AccessoryKind(string? name)
    {
        var tokens = Tokenize(Normalize(name ?? string.Empty));
        bool Has(params string[] stems) => tokens.Any(t => stems.Any(stem =>
            IsCyrillic(stem) ? t.StartsWith(stem, StringComparison.Ordinal) : MatchesLatin(t, stem)));

        if (Has("шапк", "шапоч", "ушанк", "кепк", "кепоч", "бейсболк", "панам", "шляп", "берет", "beanie", "hat", "cap", "beret"))
            return "head";
        if (Has("шарф", "снуд", "палантин", "платок", "галстук", "бабочк", "scarf", "tie", "bowtie", "necktie"))
            return "neck";
        if (Has("перчат", "варежк", "gloves", "glove", "mittens"))
            return "hands";
        if (Has("сумк", "сумоч", "рюкзак", "рюкзач", "клатч", "bag", "handbag", "tote", "backpack", "clutch"))
            return "bag";
        if (Has("ремень", "ремн", "пояс", "belt"))
            return "waist";
        return "other";
    }

    /// <summary>Рубашка или сорочка — к ним уместен галстук.</summary>
    public static bool IsShirt(string? name)
    {
        var tokens = Tokenize(Normalize(name ?? string.Empty));
        return tokens.Any(t => t.StartsWith("рубаш", StringComparison.Ordinal)
                               || t.StartsWith("сорочк", StringComparison.Ordinal)
                               || MatchesLatin(t, "shirt"));
    }

    /// <summary>Галстук или бабочка — уместны только с рубашкой, не с платьем.</summary>
    public static bool IsNeckwear(string? name)
    {
        var tokens = Tokenize(Normalize(name ?? string.Empty));
        return tokens.Any(t => t.StartsWith("галстук", StringComparison.Ordinal)
                               || t.StartsWith("бабочк", StringComparison.Ordinal)
                               || MatchesLatin(t, "tie") || MatchesLatin(t, "bowtie") || MatchesLatin(t, "necktie"));
    }

    /// <summary>Ближайший цвет словаря к RGB (без MULTI).</summary>
    public static string NearestColor((int R, int G, int B) rgb)
    {
        var max = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
        var min = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));

        // Ахроматические цвета: решаем по яркости.
        if (max - min < 24)
        {
            var lightness = (max + min) / 2;
            if (lightness < 64)
                return "BLACK";
            return lightness > 225 ? "WHITE" : "GRAY";
        }

        string best = "GRAY";
        var bestDistance = double.MaxValue;
        var references = WardrobeDictionaries.Colors
            .Where(x => x.Code != "MULTI")
            .Select(x => (x.Code, x.Hex))
            .Concat(ColorAnchors);
        // Явно цветной пиксель не сравниваем с ахроматическими (иначе «кэмел» становится серым).
        var chromatic = max - min >= 48;
        foreach (var color in references.Where(x => !(chromatic && x.Code is "BLACK" or "WHITE" or "GRAY")))
        {
            TryParseHex(color.Hex, out var candidate);
            // «Красная» метрика: человеческий глаз по-разному чувствителен к каналам.
            var rMean = (rgb.R + candidate.R) / 2.0;
            var dr = rgb.R - candidate.R;
            var dg = rgb.G - candidate.G;
            var db = rgb.B - candidate.B;
            var distance = Math.Sqrt((2 + rMean / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rMean) / 256) * db * db);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = color.Code;
            }
        }

        return best;
    }

    public static bool TryParseHex(string? hex, out (int R, int G, int B) rgb)
    {
        rgb = (0, 0, 0);
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var value = hex.Trim().TrimStart('#');
        if (value.Length == 3)
            value = string.Concat(value.Select(c => $"{c}{c}"));

        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
            return false;

        rgb = ((number >> 16) & 0xFF, (number >> 8) & 0xFF, number & 0xFF);
        return true;
    }

    /// <summary>
    /// Ищет главное существительное: в русском обычно первое («платье-рубашка» — платье),
    /// в английском — последнее («shirt dress» — платье, «dress shoes» — обувь).
    /// </summary>
    private static CategoryRule? FindCategoryRule(List<string> tokens, string text, List<string> matched)
    {
        var candidates = new List<(CategoryRule Rule, int Position, string Token, int Length, bool Cyrillic)>();

        foreach (var rule in CategoryRules)
        {
            foreach (var stem in rule.Stems)
            {
                if (IsPhrase(stem))
                {
                    var index = text.IndexOf(stem, StringComparison.Ordinal);
                    if (index < 0)
                        continue;

                    // Позиция русской фразы — её первое слово, английской — последнее (там главное слово в конце).
                    var position = CountTokensBefore(text, index);
                    if (!IsCyrillic(stem))
                        position += stem.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Length - 1;
                    candidates.Add((rule, position, stem, stem.Length, IsCyrillic(stem)));
                    continue;
                }

                for (var i = 0; i < tokens.Count; i++)
                {
                    if (MatchesNoun(tokens[i], stem, rule.ExactOnly))
                        candidates.Add((rule, i, tokens[i], stem.Length, IsCyrillic(stem)));
                }
            }
        }

        if (candidates.Count == 0)
            return null;

        var cyrillic = candidates.Where(x => x.Cyrillic).ToList();
        var best = cyrillic.Count > 0
            ? cyrillic.OrderBy(x => x.Position).ThenByDescending(x => x.Length).First()
            : candidates.OrderByDescending(x => x.Position).ThenByDescending(x => x.Length).First();

        matched.Add(best.Token);
        return best.Rule;
    }

    private static bool IsPhrase(string stem) => stem.Contains(' ') || stem.Contains('-');

    private static bool MatchesNoun(string token, string stem, bool exactOnly)
    {
        if (!IsCyrillic(stem))
            return MatchesLatin(token, stem);

        if (exactOnly)
            return token == stem;

        if (!token.StartsWith(stem, StringComparison.Ordinal))
            return false;

        // Прилагательные («джинсовая», «рубашечный») не определяют категорию.
        if (token.Length > stem.Length + 1 && AdjectiveEndings.Any(token.EndsWith))
            return false;

        return true;
    }

    /// <summary>Английские слова сравниваем целиком (с учётом множественного числа): «bag» ≠ «baggy», «boot» ≠ «bootcut».</summary>
    private static bool MatchesLatin(string token, string stem)
    {
        return token == stem || token == stem + "s" || token == stem + "es";
    }

    private static string? FindMatch(List<string> tokens, string text, string[] stems, bool exactOnly, string[]? excludes)
    {
        foreach (var stem in stems)
        {
            if (IsPhrase(stem))
            {
                if (text.Contains(stem, StringComparison.Ordinal))
                    return stem;
                continue;
            }

            var latin = !IsCyrillic(stem);
            var token = tokens.FirstOrDefault(t =>
                (latin ? MatchesLatin(t, stem) : exactOnly ? t == stem : t.StartsWith(stem, StringComparison.Ordinal))
                && (excludes is null || !excludes.Any(e => t.StartsWith(e, StringComparison.Ordinal))));

            if (token is not null)
                return token;
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.ToLowerInvariant())
        {
            builder.Append(c switch
            {
                'ё' => 'е',
                _ when char.IsLetterOrDigit(c) || c == '-' => c,
                _ => ' '
            });
        }

        var normalized = " " + string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";

        // Составные цвета склеиваем, чтобы «тёмно-синий» не превращался ещё и в «синий».
        foreach (var (from, to) in CompoundReplacements)
            normalized = normalized.Replace(from, to, StringComparison.Ordinal);

        return normalized;
    }

    private static List<string> Tokenize(string text)
    {
        return text
            .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static int CountTokensBefore(string text, int position)
    {
        return text[..position].Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static bool IsCyrillic(string token) => token.Any(c => c is >= 'а' and <= 'я');

    private static List<string> OrderSeasons(IEnumerable<string> seasons)
    {
        var order = Enum.GetNames<WardrobeSeason>().ToList();
        return seasons.Distinct().OrderBy(order.IndexOf).ToList();
    }
}
