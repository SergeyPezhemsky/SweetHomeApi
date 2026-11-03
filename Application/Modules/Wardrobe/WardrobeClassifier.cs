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
        bool ExactOnly = false);

    private sealed record ColorRule(string[] Stems, string Color, string[]? Excludes = null, bool ExactOnly = false);

    private static readonly string[] AllSeasons = [];
    private static readonly string[] Cold = [Autumn, Winter];
    private static readonly string[] Warm = [Spring, Summer];
    private static readonly string[] Demi = [Spring, Autumn];

    private static readonly CategoryRule[] CategoryRules =
    [
        // Верх
        new(["футболк", "t-shirt", "tshirt", "tee"], WardrobeCategory.TOP, 1, Warm, [Casual, Sport, Home]),
        new(["майк", "tank"], WardrobeCategory.TOP, 1, [Summer], [Casual, Sport, Home]),
        new(["топ", "топик", "top"], WardrobeCategory.TOP, 1, [Summer], [Casual], ExactOnly: true),
        new(["рубаш", "сорочк", "shirt"], WardrobeCategory.TOP, 2, AllSeasons, [Office, Casual, Formal]),
        new(["блуз", "blouse"], WardrobeCategory.TOP, 2, AllSeasons, [Office, Casual, Formal]),
        new(["поло", "polo"], WardrobeCategory.TOP, 2, Warm, [Casual, Sport], ExactOnly: true),
        new(["лонгслив", "longsleeve"], WardrobeCategory.TOP, 2, AllSeasons, [Casual, Home]),
        new(["водолазк", "turtleneck"], WardrobeCategory.TOP, 3, Cold, [Casual, Office]),
        new(["свитер", "sweater"], WardrobeCategory.TOP, 4, Cold, [Casual]),
        new(["джемпер", "jumper", "пуловер", "pullover"], WardrobeCategory.TOP, 3, [Spring, Autumn, Winter], [Casual, Office]),
        new(["кофт", "кардиган", "cardigan"], WardrobeCategory.TOP, 3, [Spring, Autumn, Winter], [Casual, Office, Home]),
        new(["худи", "hoodie", "толстовк", "свитшот", "sweatshirt"], WardrobeCategory.TOP, 3, [Spring, Autumn, Winter], [Casual, Sport, Home]),

        // Низ
        new(["джинсы", "джинс", "jeans"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual], ExactOnly: true),
        new(["брюк", "trousers", "slacks"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Office, Casual, Formal]),
        new(["штан", "pants", "карго", "cargo", "чинос", "chinos"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual]),
        new(["джоггер", "joggers", "треник"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual, Sport, Home]),
        new(["шорт", "shorts"], WardrobeCategory.BOTTOM, 1, [Summer], [Casual, Sport, Home]),
        new(["юбк", "skirt"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Casual, Office]),
        new(["легинс", "леггинс", "лосин", "leggings", "тайтс"], WardrobeCategory.BOTTOM, 2, AllSeasons, [Sport, Home, Casual]),

        // Платья
        new(["плать", "dress"], WardrobeCategory.DRESS, 2, AllSeasons, [Casual, Office, Formal]),
        new(["сарафан", "sundress"], WardrobeCategory.DRESS, 1, [Summer], [Casual]),
        new(["комбинезон", "jumpsuit"], WardrobeCategory.DRESS, 2, AllSeasons, [Casual]),

        // Верхняя одежда
        new(["пуховик", "puffer", "down jacket", "дубленк", "шуб", "parka", "парка"], WardrobeCategory.OUTERWEAR, 5, [Winter], [Casual]),
        new(["пальто", "coat", "полупальто"], WardrobeCategory.OUTERWEAR, 4, [Spring, Autumn, Winter], [Casual, Office, Formal]),
        new(["куртк", "jacket", "анорак", "anorak"], WardrobeCategory.OUTERWEAR, 4, [Spring, Autumn, Winter], [Casual]),
        new(["плащ", "тренч", "trench", "raincoat"], WardrobeCategory.OUTERWEAR, 3, Demi, [Casual, Office]),
        new(["бомбер", "bomber", "косух", "ветровк", "windbreaker"], WardrobeCategory.OUTERWEAR, 3, Demi, [Casual, Sport]),
        new(["пиджак", "жакет", "блейзер", "blazer"], WardrobeCategory.OUTERWEAR, 2, [Spring, Summer, Autumn], [Office, Formal]),
        new(["жилет", "vest"], WardrobeCategory.OUTERWEAR, 3, Demi, [Casual]),

        // Обувь
        new(["кроссовк", "sneaker", "trainer", "runner"], WardrobeCategory.SHOES, 2, [Spring, Summer, Autumn], [Casual, Sport]),
        new(["кеды", "кед", "converse"], WardrobeCategory.SHOES, 2, Warm, [Casual], ExactOnly: true),
        new(["ботин", "boot", "челси", "chelsea", "берц", "тимберленд"], WardrobeCategory.SHOES, 4, [Spring, Autumn, Winter], [Casual]),
        new(["сапог", "угги", "ugg", "дутик"], WardrobeCategory.SHOES, 5, [Winter], [Casual]),
        new(["туфл", "лодочк", "шпильк", "heels", "pumps", "oxford", "оксфорд", "дерби", "derby"], WardrobeCategory.SHOES, 2, [Spring, Summer, Autumn], [Office, Formal]),
        new(["лофер", "loafer", "мокасин", "moccasin", "балетк", "ballet"], WardrobeCategory.SHOES, 2, [Spring, Summer, Autumn], [Casual, Office]),
        new(["босонож", "сандал", "sandal", "шлеп", "сланц", "flip-flop", "вьетнамк"], WardrobeCategory.SHOES, 1, [Summer], [Casual]),
        new(["тапк", "тапоч", "slipper"], WardrobeCategory.SHOES, 1, AllSeasons, [Home]),

        // Аксессуары
        new(["шапк", "beanie", "ушанк"], WardrobeCategory.ACCESSORY, 4, [Winter], [Casual]),
        new(["шарф", "снуд", "scarf"], WardrobeCategory.ACCESSORY, 4, Cold, [Casual, Office]),
        new(["перчат", "варежк", "gloves", "mittens"], WardrobeCategory.ACCESSORY, 4, [Winter], [Casual, Office]),
        new(["кепк", "бейсболк", "панам", "cap", "панама"], WardrobeCategory.ACCESSORY, 1, [Summer], [Casual, Sport]),
        new(["шляп", "hat", "берет", "beret"], WardrobeCategory.ACCESSORY, 2, AllSeasons, [Casual]),
        new(["галстук", "tie", "бабочк", "bowtie"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Office, Formal]),
        new(["ремень", "ремн", "пояс", "belt"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Casual, Office, Formal]),
        new(["сумк", "рюкзак", "клатч", "bag", "backpack", "clutch"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Casual, Office]),
        new(["очки", "sunglasses", "часы", "watch", "платок", "серьг", "колье", "браслет"], WardrobeCategory.ACCESSORY, 1, AllSeasons, [Casual, Office, Formal])
    ];

    private static readonly ModifierRule[] ModifierRules =
    [
        new(["тепл", "утепл", "warm", "insulated", "thermal", "термо"], WarmthDelta: 1),
        new(["шерст", "wool", "кашемир", "cashmere", "флис", "fleece", "мохер", "ангор", "вязан", "knit"], WarmthDelta: 1, Seasons: Cold),
        new(["зимн", "winter"], WarmthDelta: 1, Seasons: [Winter]),
        new(["летн", "summer"], WarmthDelta: -1, Seasons: [Summer]),
        new(["легк", "тонк", "light", "thin"], WarmthDelta: -1),
        new(["льнян", "linen", "шелк", "silk", "сетк"], WarmthDelta: -1, Seasons: Warm),
        new(["лен"], WarmthDelta: -1, Seasons: Warm, ExactOnly: true),
        new(["демисезон"], Seasons: Demi),
        new(["осенн", "autumn", "fall"], Seasons: [Autumn]),
        new(["весенн", "spring"], Seasons: [Spring]),
        new(["спорт", "sport", "трениров", "беговы", "running", "фитнес", "gym", "йог", "yoga"], Styles: [Sport]),
        new(["офис", "делов", "office", "business", "классич", "classic"], Styles: [Office]),
        new(["вечерн", "праздн", "коктейл", "evening", "formal", "party", "нарядн"], Styles: [Formal]),
        new(["домашн", "home", "пижам", "pajama", "lounge"], Styles: [Home])
    ];

    private static readonly ColorRule[] ColorRules =
    [
        new(["темносин", "navy", "нэви", "индиго"], "NAVY"),
        new(["черн", "black"], "BLACK"),
        new(["бел", "white", "молочн", "кремов", "ivory", "cream"], "WHITE", Excludes: ["бель", "белорус"]),
        new(["сер", "gray", "grey", "графит", "антрацит"], "GRAY", Excludes: ["серьг", "серд", "серебр", "серия", "серии"]),
        new(["серебр", "silver"], "GRAY"),
        new(["беж", "beige", "песочн", "камел", "camel", "тауп"], "BEIGE"),
        new(["коричн", "brown", "шоколад", "кофейн", "терракот"], "BROWN"),
        new(["голуб", "lightblue"], "LIGHT_BLUE"),
        new(["син", "blue", "джинсов", "denim", "деним"], "BLUE", Excludes: ["синтет", "синтеп"]),
        new(["бордов", "винн", "burgundy", "maroon", "марсал"], "BURGUNDY"),
        new(["красн", "red", "алый"], "RED"),
        new(["розов", "pink", "пудров", "фукси"], "PINK"),
        new(["оранж", "orange", "морков", "коралл"], "ORANGE"),
        new(["желт", "yellow", "горчичн", "mustard", "лимонн"], "YELLOW"),
        new(["оливк", "olive", "хаки", "khaki", "милитари"], "OLIVE"),
        new(["зелен", "green", "изумруд", "мятн", "салатов"], "GREEN"),
        new(["фиолет", "purple", "violet", "сиренев", "лилов", "lilac", "лаванд"], "PURPLE"),
        new(["принт", "print", "цветочн", "floral", "клетк", "клетчат", "plaid", "полоск", "striped", "пестр", "разноцвет", "камуфляж"], "MULTI")
    ];

    private static readonly (string From, string To)[] CompoundReplacements =
    [
        ("темно-син", "темносин"),
        ("темно син", "темносин"),
        ("dark blue", "navy"),
        ("light blue", "lightblue"),
        ("light-blue", "lightblue"),
        ("sky blue", "lightblue")
    ];

    private static readonly string[] AdjectiveEndings =
        ["ый", "ий", "ой", "ая", "яя", "ое", "ее", "ые", "ие", "ого", "его", "ому", "ему", "ую", "юю", "ых", "их", "ым", "им"];

    public static WardrobeClassification Classify(string? name, string? notes = null, string? colorHex = null)
    {
        var text = Normalize($"{name} {notes}");
        var tokens = Tokenize(text);
        var result = new WardrobeClassification();
        var matched = new List<string>();

        var categoryRule = FindCategoryRule(tokens, text, matched);
        var seasons = new HashSet<string>(categoryRule?.Seasons ?? []);
        var styles = new HashSet<string>(categoryRule?.Styles ?? []);
        var explicitSeasons = new HashSet<string>();
        var explicitStyles = new HashSet<string>();
        var warmth = categoryRule?.Warmth ?? 0;
        var signals = 0;

        foreach (var modifier in ModifierRules)
        {
            var token = FindMatch(tokens, text, modifier.Stems, modifier.ExactOnly, null);
            if (token is null)
                continue;

            matched.Add(token);
            signals++;
            warmth += modifier.WarmthDelta;
            foreach (var season in modifier.Seasons ?? [])
                explicitSeasons.Add(season);
            foreach (var style in modifier.Styles ?? [])
                explicitStyles.Add(style);
        }

        if (explicitSeasons.Count > 0)
            seasons = explicitSeasons;

        if (explicitStyles.Count > 0)
        {
            // Явно названный стиль ставим первым, остальные стили категории оставляем как совместимые.
            styles = [..explicitStyles, ..styles.Where(x => !explicitStyles.Contains(x) && x != Home)];
        }

        var colors = new List<string>();
        foreach (var rule in ColorRules)
        {
            var token = FindMatch(tokens, text, rule.Stems, rule.ExactOnly, rule.Excludes);
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
            return lightness > 205 ? "WHITE" : "GRAY";
        }

        string best = "GRAY";
        var bestDistance = double.MaxValue;
        foreach (var color in WardrobeDictionaries.Colors.Where(x => x.Code != "MULTI"))
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

    private static CategoryRule? FindCategoryRule(List<string> tokens, string text, List<string> matched)
    {
        CategoryRule? best = null;
        var bestPosition = int.MaxValue;
        string? bestToken = null;

        foreach (var rule in CategoryRules)
        {
            foreach (var stem in rule.Stems)
            {
                int position;
                string? token;

                if (stem.Contains(' ') || stem.Contains('-'))
                {
                    position = text.IndexOf(stem, StringComparison.Ordinal);
                    token = position >= 0 ? stem : null;
                    if (token is not null)
                        position = CountTokensBefore(text, position);
                }
                else
                {
                    position = tokens.FindIndex(t => MatchesNoun(t, stem, rule.ExactOnly));
                    token = position >= 0 ? tokens[position] : null;
                }

                if (token is null || position >= bestPosition)
                    continue;

                best = rule;
                bestPosition = position;
                bestToken = token;
            }
        }

        if (bestToken is not null)
            matched.Add(bestToken);

        return best;
    }

    private static bool MatchesNoun(string token, string stem, bool exactOnly)
    {
        if (exactOnly)
            return token == stem;

        if (!token.StartsWith(stem, StringComparison.Ordinal))
            return false;

        // Прилагательные («джинсовая», «рубашечный») не определяют категорию.
        if (token.Length > stem.Length + 1 && IsCyrillic(token) && AdjectiveEndings.Any(token.EndsWith))
            return false;

        return true;
    }

    private static string? FindMatch(List<string> tokens, string text, string[] stems, bool exactOnly, string[]? excludes)
    {
        foreach (var stem in stems)
        {
            if (stem.Contains(' ') || stem.Contains('-'))
            {
                if (text.Contains(stem, StringComparison.Ordinal))
                    return stem;
                continue;
            }

            var token = tokens.FirstOrDefault(t =>
                (exactOnly ? t == stem : t.StartsWith(stem, StringComparison.Ordinal))
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
