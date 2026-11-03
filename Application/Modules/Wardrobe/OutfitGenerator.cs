using Application.Modules.Wardrobe.Seeds;

namespace Application.Modules.Wardrobe;

public class OutfitSuggestionRequest
{
    public WardrobeStyle Occasion { get; set; } = WardrobeStyle.CASUAL;

    /// <summary>Температура воздуха, °C. Если не задана — берётся типичная для текущего сезона.</summary>
    public int? Temperature { get; set; }

    public int Count { get; set; } = 3;
    public int? Seed { get; set; }
}

public class OutfitSuggestion
{
    public List<WardrobeItem> Items { get; set; } = [];
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = [];
}

public class OutfitSuggestionResult
{
    public List<OutfitSuggestion> Outfits { get; set; } = [];
    public int TargetWarmth { get; set; }
    public WardrobeSeason Season { get; set; }
    public int Temperature { get; set; }
    public bool TemperatureAssumed { get; set; }
    public List<string> Missing { get; set; } = [];
}

/// <summary>
/// Детерминированный подбор образов: погода → допустимые вещи → шаблоны образов → оценка → разнообразная выдача.
/// </summary>
public static class OutfitGenerator
{
    public const int OuterwearRequiredBelow = 15;
    public const int OuterwearForbiddenAbove = 22;

    private const int CandidatesPerSlot = 10;
    private const int MaxCandidates = 50_000;
    private const int ReusePenalty = 8;
    private const int AccessoryBonus = 2;
    private const int MaxScoreGap = 20;
    private const int AccessoryCandidates = 400;

    /// <summary>Ниже этой температуры верхняя одежда должна быть самой тёплой (пуховик, шуба).</summary>
    public const int SevereFrostBelow = -10;

    private enum OuterwearMode
    {
        Required,
        Optional,
        Forbidden
    }

    private sealed class Candidate
    {
        public required List<WardrobeItem> Items { get; init; }
        public required double Score { get; init; }
        public required string Key { get; init; }
    }

    public static OutfitSuggestionResult Generate(IEnumerable<WardrobeItem> wardrobe, OutfitSuggestionRequest request, DateTime nowUtc)
    {
        var count = Math.Clamp(request.Count, 1, 10);
        var seed = request.Seed ?? 0;
        var temperatureAssumed = request.Temperature is null;
        var season = ResolveSeason(request.Temperature, nowUtc);
        var temperature = request.Temperature ?? TypicalTemperature(nowUtc);

        // «Дом» — образ для помещения: уличная погода не важна, верхняя одежда не нужна.
        var indoor = request.Occasion == WardrobeStyle.HOME;
        var effectiveTemperature = indoor ? IndoorTemperature : temperature;
        var targetWarmth = TargetWarmth(effectiveTemperature);
        var desired = DesiredWarmth(effectiveTemperature);
        var baseDesired = Math.Min(desired, 4.0);
        var outerwearMode = indoor
            ? OuterwearMode.Forbidden
            : temperature < OuterwearRequiredBelow
                ? OuterwearMode.Required
                : temperature > OuterwearForbiddenAbove ? OuterwearMode.Forbidden : OuterwearMode.Optional;

        var result = new OutfitSuggestionResult
        {
            TargetWarmth = targetWarmth,
            Season = season,
            Temperature = temperature,
            TemperatureAssumed = temperatureAssumed
        };

        var occasion = request.Occasion.ToString();
        var allowedSeasons = indoor
            ? Enum.GetNames<WardrobeSeason>().ToHashSet()
            : AllowedSeasons(temperature, temperature, season);
        var inSeason = wardrobe
            .Where(x => x.Seasons.Count == 0 || x.Seasons.Any(allowedSeasons.Contains))
            .ToList();
        var suitable = inSeason.Where(x => x.Styles.Contains(occasion)).ToList();

        // Уличные вещи (верхняя одежда, обувь, тёплые аксессуары): если для повода своих нет,
        // берём повседневные — зимние ботинки в офис лучше, чем никакой подборки.
        var casual = nameof(WardrobeStyle.CASUAL);
        List<WardrobeItem> StreetPool(WardrobeCategory category, Func<WardrobeItem, bool> fits)
        {
            var own = suitable.Where(x => x.Category == category && fits(x)).ToList();
            if (own.Count > 0 || indoor)
                return own;

            return inSeason.Where(x => x.Category == category && x.Styles.Contains(casual) && fits(x)).ToList();
        }

        List<WardrobeItem> Best(IEnumerable<WardrobeItem> pool, double want) => pool
            .OrderByDescending(x => ItemScore(x, want, nowUtc, seed))
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Take(CandidatesPerSlot)
            .ToList();

        // Допуск по теплоте базового слоя: в тепло ±1.2 (без утеплённых брюк при +20), в мороз ±1.5
        // (без лёгкого платья под пуховик), в остальное время ±2.
        // Для низа в мороз оставляем ±2: джинсы под пуховиком зимой — нормально.
        // Строгий морозный допуск — только для уличных поводов: в офис и на праздник под пальто носят рубашку.
        var streetOccasion = request.Occasion is WardrobeStyle.CASUAL or WardrobeStyle.SPORT;
        var baseTolerance = desired <= 2.5 ? 1.2 : desired >= 4.5 && streetOccasion ? 1.5 : 2.0;
        var bottomTolerance = desired <= 2.5 ? 1.2 : 2.0;
        bool BaseFits(WardrobeItem x) => Math.Abs(x.Warmth - baseDesired) <= baseTolerance;
        bool BottomFits(WardrobeItem x) => Math.Abs(x.Warmth - baseDesired) <= bottomTolerance;
        // В сильный мороз — только тёплая обувь (не туфли и не кроссовки).
        // В лютый мороз (ниже −20) — только самая тёплая обувь, если она есть в гардеробе.
        var minShoeWarmth = indoor || temperature >= SevereFrostBelow ? 1
            : temperature < -20 && inSeason.Any(x => x.Category == WardrobeCategory.SHOES && x.Warmth >= 5) ? 5
            : 4;
        bool ShoesFit(WardrobeItem x) => Math.Abs(x.Warmth - desired) <= (indoor ? 1.5 : 2.0) && x.Warmth >= minShoeWarmth;

        var tops = Best(suitable.Where(x => x.Category == WardrobeCategory.TOP && BaseFits(x)), baseDesired);
        var bottoms = Best(suitable.Where(x => x.Category == WardrobeCategory.BOTTOM && BottomFits(x)), baseDesired);
        var dresses = Best(suitable.Where(x => x.Category == WardrobeCategory.DRESS && BaseFits(x)), baseDesired);
        var shoes = Best(StreetPool(WardrobeCategory.SHOES, ShoesFit), desired);
        var minOuterWarmth = MinOuterwearWarmth(temperature, targetWarmth);
        var outerwear = outerwearMode == OuterwearMode.Forbidden
            ? []
            : Best(StreetPool(WardrobeCategory.OUTERWEAR, x => x.Warmth >= minOuterWarmth && x.Warmth <= targetWarmth + 1), desired);
        // Тёплые аксессуары подбираем по «местам»: если офисной шапки нет, берём повседневную,
        // даже если офисные шарф и перчатки есть.
        var warmAccessories = new List<WardrobeItem>();
        if (desired >= 3.5 && !indoor)
        {
            foreach (var kind in new[] { "head", "neck", "hands" })
            {
                bool Warm(WardrobeItem x) => x.Warmth >= 4 && WardrobeClassifier.AccessoryKind(x.Name) == kind;
                var own = suitable.Where(x => x.Category == WardrobeCategory.ACCESSORY && Warm(x)).ToList();
                warmAccessories.AddRange(own.Count > 0
                    ? own
                    : inSeason.Where(x => x.Category == WardrobeCategory.ACCESSORY && x.Styles.Contains(casual) && Warm(x)));
            }
        }

        var accessories = Best(
            suitable.Where(x => x.Category == WardrobeCategory.ACCESSORY && Math.Abs(x.Warmth - desired) <= 2)
                .Concat(warmAccessories)
                .DistinctBy(x => x.Id),
            desired);

        FillMissing(result, request.Occasion, season, targetWarmth, minOuterWarmth, outerwearMode, tops, bottoms, dresses, shoes, outerwear);
        if (result.Missing.Count > 0)
            return result;

        var outerOptions = outerwearMode switch
        {
            OuterwearMode.Required => outerwear.Select(x => (WardrobeItem?)x).ToList(),
            OuterwearMode.Forbidden => [null],
            _ => outerwear.Select(x => (WardrobeItem?)x).Prepend(null).ToList()
        };

        // В межсезонье куртка тем менее уместна, чем теплее: при +22 почти всегда без неё.
        var optionalOuterPenalty = outerwearMode == OuterwearMode.Optional
            ? 8.0 * (temperature - OuterwearRequiredBelow + 1) / (OuterwearForbiddenAbove - OuterwearRequiredBelow + 1)
            : 0;

        var bases = new List<List<WardrobeItem>>();
        foreach (var top in tops)
        foreach (var bottom in bottoms)
            bases.Add([top, bottom]);
        foreach (var dress in dresses)
            bases.Add([dress]);

        var context = new ScoreContext(baseDesired, desired, nowUtc, seed);

        // Сначала оцениваем образы без аксессуаров, затем подбираем аксессуары только лучшим —
        // так перебор не умножается на число аксессуаров.
        var draft = new List<(List<WardrobeItem> Items, double Score)>();
        foreach (var baseItems in bases)
        foreach (var shoe in shoes)
        foreach (var outer in outerOptions)
        {
            if (draft.Count >= MaxCandidates)
                break;

            var items = new List<WardrobeItem>(baseItems) { shoe };
            if (outer is not null)
                items.Add(outer);

            var score = OutfitScore(items, context) - (outer is not null ? optionalOuterPenalty : 0);
            draft.Add((items, score));
        }

        // В холод допускаем комплект тёплых аксессуаров (шапка + шарф + перчатки), иначе — один.
        var maxAccessories = desired >= 3.5 && !indoor ? 3 : 1;

        var candidates = new List<Candidate>();
        foreach (var (items, baseScore) in draft
                     .OrderByDescending(x => x.Score)
                     .ThenBy(x => string.Join('|', x.Items.Select(i => i.Id)), StringComparer.Ordinal)
                     .Take(AccessoryCandidates))
        {
            var score = baseScore;
            // Галстук — только к рубашке (не к платью, водолазке или футболке).
            var tieAllowed = items.Any(x => x.Category == WardrobeCategory.TOP && WardrobeClassifier.IsShirt(x.Name));
            var outerPenalty = items.Any(x => x.Category == WardrobeCategory.OUTERWEAR) ? optionalOuterPenalty : 0;
            var kinds = new HashSet<string>();
            var added = 0;

            // Аксессуары добавляем по одному на «место» (голова, шея, руки, сумка), пока они не снижают оценку.
            while (added < maxAccessories)
            {
                WardrobeItem? bestAccessory = null;
                var bestWithAccessory = score;
                foreach (var accessory in accessories)
                {
                    if (items.Contains(accessory) || (!tieAllowed && WardrobeClassifier.IsNeckwear(accessory.Name)))
                        continue;

                    var kind = WardrobeClassifier.AccessoryKind(accessory.Name);
                    if (kinds.Contains(kind))
                        continue;

                    // Бонус за законченность — только тёплым аксессуарам в холод (шапка, шарф, перчатки).
                    // Остальные (ремень, сумка, часы) добавляем, только если они действительно улучшают образ.
                    var warmSet = maxAccessories > 1 && accessory.Warmth >= 4 && kind is "head" or "neck" or "hands";
                    var withAccessory = OutfitScore([..items, accessory], context) - outerPenalty
                                        + (warmSet ? AccessoryBonus * (added + 1) : 0);
                    if (warmSet ? withAccessory >= bestWithAccessory : withAccessory > bestWithAccessory + 0.5)
                    {
                        bestWithAccessory = withAccessory;
                        bestAccessory = accessory;
                    }
                }

                if (bestAccessory is null)
                    break;

                items.Add(bestAccessory);
                kinds.Add(WardrobeClassifier.AccessoryKind(bestAccessory.Name));
                score = bestWithAccessory;
                added++;
            }

            candidates.Add(new Candidate
            {
                Items = items,
                Score = score,
                Key = string.Join('|', items.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal))
            });
        }

        var ordered = candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        // Ради разнообразия не опускаемся до заметно худших образов.
        if (ordered.Count > 0)
        {
            var floor = ordered[0].Score - MaxScoreGap;
            ordered = ordered.Where(x => x.Score >= floor).ToList();
        }

        var used = new Dictionary<string, int>();
        var pickedKeys = new HashSet<string>();
        for (var i = 0; i < count; i++)
        {
            Candidate? best = null;
            var bestAdjusted = double.MinValue;
            foreach (var candidate in ordered)
            {
                if (pickedKeys.Contains(candidate.Key))
                    continue;

                var reuse = candidate.Items.Sum(x => used.GetValueOrDefault(x.Id));
                var adjusted = candidate.Score - ReusePenalty * reuse;
                if (adjusted > bestAdjusted)
                {
                    bestAdjusted = adjusted;
                    best = candidate;
                }
            }

            if (best is null)
                break;

            pickedKeys.Add(best.Key);
            foreach (var item in best.Items)
                used[item.Id] = used.GetValueOrDefault(item.Id) + 1;

            result.Outfits.Add(new OutfitSuggestion
            {
                Items = SortItems(best.Items),
                Score = (int)Math.Round(Math.Clamp(best.Score, 0, 100)),
                Reasons = indoor
                    ? BuildIndoorReasons(best.Items, nowUtc)
                    : BuildReasons(best.Items, temperature, temperatureAssumed, nowUtc)
            });
        }

        // Штраф за повтор вещей влияет на выбор, но показываем варианты от лучшего к худшему.
        result.Outfits = result.Outfits.OrderByDescending(x => x.Score).ToList();
        return result;
    }

    /// <summary>Комнатная температура для повода «Дом».</summary>
    public const int IndoorTemperature = 22;

    /// <summary>
    /// Непрерывная «нужная теплота» 1..5: +25 °C → 1, +15 → 2, +5 → 3, −5 → 4, −15 и ниже → 5.
    /// </summary>
    public static double DesiredWarmth(int temperature) => Math.Clamp(1 + (25 - temperature) / 10.0, 1, 5);

    public static int TargetWarmth(int temperature) => temperature switch
    {
        >= 25 => 1,
        >= 18 => 2,
        >= 10 => 3,
        >= 0 => 4,
        _ => 5
    };

    /// <summary>
    /// Сезоны, вещи которых уместны при такой погоде. Решает температура, а не календарь:
    /// при +19 °C в ноябре подходят и летние футболки, при +1 °C в марте — и зимний свитер.
    /// </summary>
    public static HashSet<string> AllowedSeasons(int? requestedTemperature, int temperature, WardrobeSeason season)
    {
        if (requestedTemperature is null)
        {
            var byMonth = new HashSet<string> { season.ToString() };
            if (season is WardrobeSeason.SPRING or WardrobeSeason.AUTUMN)
            {
                byMonth.Add(nameof(WardrobeSeason.SPRING));
                byMonth.Add(nameof(WardrobeSeason.AUTUMN));
            }

            return byMonth;
        }

        var seasons = new HashSet<string>();
        if (temperature >= 15)
            seasons.Add(nameof(WardrobeSeason.SUMMER));
        if (temperature is > 0 and < 22)
        {
            seasons.Add(nameof(WardrobeSeason.SPRING));
            seasons.Add(nameof(WardrobeSeason.AUTUMN));
        }
        if (temperature <= 5)
            seasons.Add(nameof(WardrobeSeason.WINTER));

        return seasons;
    }

    /// <summary>
    /// Минимальная теплота верхней одежды: в сильный мороз — только «очень тёплая»,
    /// иначе допускаем на ступень легче нужной.
    /// </summary>
    public static int MinOuterwearWarmth(int temperature, int targetWarmth)
    {
        return temperature < SevereFrostBelow ? 5 : Math.Max(1, targetWarmth - 1);
    }

    public static WardrobeSeason ResolveSeason(int? temperature, DateTime nowUtc)
    {
        var month = nowUtc.Month;
        if (temperature is null)
        {
            return month switch
            {
                12 or 1 or 2 => WardrobeSeason.WINTER,
                >= 3 and <= 5 => WardrobeSeason.SPRING,
                >= 6 and <= 8 => WardrobeSeason.SUMMER,
                _ => WardrobeSeason.AUTUMN
            };
        }

        if (temperature >= 20)
            return WardrobeSeason.SUMMER;
        if (temperature <= 0)
            return WardrobeSeason.WINTER;

        return month <= 6 ? WardrobeSeason.SPRING : WardrobeSeason.AUTUMN;
    }

    /// <summary>Типичная дневная температура в Москве по месяцам (используется, если погода не задана).</summary>
    private static readonly int[] MoscowMonthlyTemperature = [-6, -5, 0, 9, 17, 21, 23, 21, 15, 8, 1, -4];

    public static int TypicalTemperature(DateTime nowUtc) => MoscowMonthlyTemperature[nowUtc.Month - 1];

    public static int TypicalTemperature(WardrobeSeason season) => season switch
    {
        WardrobeSeason.WINTER => -5,
        WardrobeSeason.SPRING => 12,
        WardrobeSeason.SUMMER => 24,
        _ => 8
    };

    private static void FillMissing(
        OutfitSuggestionResult result,
        WardrobeStyle occasion,
        WardrobeSeason season,
        int targetWarmth,
        int minOuterWarmth,
        OuterwearMode outerwearMode,
        List<WardrobeItem> tops,
        List<WardrobeItem> bottoms,
        List<WardrobeItem> dresses,
        List<WardrobeItem> shoes,
        List<WardrobeItem> outerwear)
    {
        var context = $"для стиля «{WardrobeDictionaries.StyleName(occasion)}» (сезон «{WardrobeDictionaries.SeasonName(season).ToLowerInvariant()}»)";
        var hasBase = (tops.Count > 0 && bottoms.Count > 0) || dresses.Count > 0;

        if (!hasBase)
        {
            if (tops.Count == 0 && bottoms.Count == 0)
                result.Missing.Add($"Добавьте верх и низ или платье {context}");
            else if (tops.Count == 0)
                result.Missing.Add($"Добавьте верх {context}");
            else
                result.Missing.Add($"Добавьте низ {context}");
        }

        if (shoes.Count == 0)
            result.Missing.Add($"Добавьте обувь {context}");

        if (outerwearMode == OuterwearMode.Required && outerwear.Count == 0)
        {
            var from = WardrobeDictionaries.WarmthLevels.First(x => x.Level == minOuterWarmth).Name.ToLowerInvariant();
            var to = WardrobeDictionaries.WarmthLevels.First(x => x.Level == Math.Min(5, targetWarmth + 1)).Name.ToLowerInvariant();
            var range = from == to ? $"«{from}»" : $"от «{from}» до «{to}»";
            result.Missing.Add($"Добавьте верхнюю одежду {context} — по теплоте {range}");
        }
    }

    private sealed record ScoreContext(double BaseDesired, double Desired, DateTime NowUtc, int Seed);

    private static double ItemScore(WardrobeItem item, double desiredWarmth, DateTime nowUtc, int seed)
    {
        return 10 - 3 * Math.Abs(item.Warmth - desiredWarmth)
               + 4 * Freshness(item, nowUtc)
               + (item.IsFavorite ? 2 : 0)
               + Jitter(item.Id, seed);
    }

    private static double OutfitScore(List<WardrobeItem> items, ScoreContext context)
    {
        // Теплота: 35 баллов. Нужная теплота непрерывна по температуре: +10 и +17 — разные образы.
        var warmthPenalty = items.Sum(x =>
        {
            var desired = x.Category is WardrobeCategory.OUTERWEAR or WardrobeCategory.SHOES or WardrobeCategory.ACCESSORY
                ? context.Desired
                : context.BaseDesired;
            return Math.Abs(x.Warmth - desired);
        });
        var warmthScore = Math.Max(0, 35 - 14.0 * warmthPenalty / items.Count);

        // Согласованность «улицы»: лёгкая куртка с зимними сапогами и шапкой выглядит странно.
        var street = items
            .Where(x => x.Category is WardrobeCategory.OUTERWEAR or WardrobeCategory.SHOES
                        || (x.Category == WardrobeCategory.ACCESSORY && x.Warmth >= 4))
            .Select(x => x.Warmth)
            .ToList();
        var coherencePenalty = street.Count >= 2 ? 8.0 * Math.Max(0, street.Max() - street.Min() - 1) : 0;

        // Цвет: 35 баллов.
        var colorScore = ColorHarmony(items);

        // Свежесть: 20 баллов.
        var freshnessScore = 20 * items.Average(x => Freshness(x, context.NowUtc));

        // Любимые вещи: 10 баллов.
        var favoriteScore = 10.0 * items.Count(x => x.IsFavorite) / items.Count;

        var jitter = items.Sum(x => Jitter(x.Id, context.Seed)) / items.Count * 2;
        return warmthScore - coherencePenalty + colorScore + freshnessScore + favoriteScore + jitter;
    }

    public static double ColorHarmony(IReadOnlyCollection<WardrobeItem> items)
    {
        var colors = items.SelectMany(x => x.Colors.Take(2)).ToList();
        // Оттенки одного цвета (синий и голубой, красный и бордовый) считаем одним акцентом.
        var accents = colors
            .Where(x => WardrobeDictionaries.FindColor(x) is { Neutral: false })
            .Where(x => x != "MULTI")
            .Select(ColorFamily)
            .Distinct()
            .ToList();
        var multiCount = items.Count(x => x.Colors.Contains("MULTI"));

        double score = accents.Count switch
        {
            0 => 28,
            1 => 35,
            2 => 27,
            3 => 14,
            _ => 4
        };

        if (multiCount > 1)
            score -= 15;
        else if (multiCount == 1 && accents.Count >= 2)
            score -= 8;

        foreach (var (a, b) in ClashingPairs)
        {
            if (accents.Contains(a) && accents.Contains(b))
                score -= 8;
        }

        return Math.Max(0, score);
    }

    private static readonly (string, string)[] ClashingPairs =
    [
        ("RED", "PINK"),
        ("RED", "ORANGE"),
        ("PINK", "ORANGE"),
        ("RED", "GREEN"),
        ("PURPLE", "ORANGE"),
        ("YELLOW", "PURPLE"),
        ("GREEN", "PINK")
    ];

    private static string ColorFamily(string code) => code switch
    {
        "LIGHT_BLUE" => "BLUE",
        "BURGUNDY" => "RED",
        "OLIVE" => "GREEN",
        _ => code
    };

    /// <summary>0 — надевали в последние 3 дня, 1 — давно не надевали или не надевали никогда.</summary>
    private static double Freshness(WardrobeItem item, DateTime nowUtc)
    {
        // Небольшая поправка за то, как часто вещь вообще носят: редко надеваемые всплывают чаще.
        var rarity = 1 - Math.Min(item.WearCount, 30) / 30.0;
        if (item.LastWornAt is null)
            return 0.85 + 0.15 * rarity;

        // Целые дни: результат не меняется от часа к часу при одинаковом запросе.
        var days = Math.Floor((nowUtc - item.LastWornAt.Value).TotalDays);
        double recency = days < 3 ? 0 : days >= 14 ? 1 : (days - 3) / 11;
        return 0.85 * recency + 0.15 * rarity;
    }

    /// <summary>Стабильный «шум» −1..+1 от id вещи и seed: разнообразит выдачу, но повторяем.</summary>
    private static double Jitter(string id, int seed)
    {
        unchecked
        {
            var hash = (uint)(2166136261 ^ seed);
            foreach (var c in id)
            {
                hash ^= c;
                hash *= 16777619;
            }

            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            hash ^= hash >> 15;
            return hash / (double)uint.MaxValue * 2 - 1;
        }
    }

    private static List<WardrobeItem> SortItems(IEnumerable<WardrobeItem> items)
    {
        var order = new[]
        {
            WardrobeCategory.OUTERWEAR, WardrobeCategory.TOP, WardrobeCategory.DRESS,
            WardrobeCategory.BOTTOM, WardrobeCategory.SHOES, WardrobeCategory.ACCESSORY
        };
        return items.OrderBy(x => Array.IndexOf(order, x.Category)).ToList();
    }

    private static List<string> BuildIndoorReasons(List<WardrobeItem> items, DateTime nowUtc)
    {
        var reasons = new List<string> { "Для дома: удобно и без верхней одежды" };
        var favorite = items.FirstOrDefault(x => x.IsFavorite);
        if (favorite is not null)
            reasons.Add($"Любимая вещь: {LowerFirst(favorite.Name)}");
        return reasons;
    }

    private static List<string> BuildReasons(List<WardrobeItem> items, int temperature, bool temperatureAssumed, DateTime nowUtc)
    {
        var reasons = new List<string>();
        var temperatureLabel = temperatureAssumed ? $"около {temperature}°C (типично для сезона)" : $"{temperature}°C";
        var outer = items.FirstOrDefault(x => x.Category == WardrobeCategory.OUTERWEAR);

        reasons.Add(outer is not null
            ? $"По погоде {temperatureLabel}: {LowerFirst(outer.Name)} сверху"
            : $"По погоде {temperatureLabel}: без верхней одежды");

        var accents = items.SelectMany(x => x.Colors.Take(2))
            .Where(x => WardrobeDictionaries.FindColor(x) is { Neutral: false } && x != "MULTI")
            .Distinct()
            .Select(WardrobeDictionaries.ColorName)
            .ToList();

        reasons.Add(accents.Count switch
        {
            0 => "Спокойная нейтральная гамма",
            1 => $"Один акцентный цвет — {accents[0]}, остальное нейтральное",
            2 => $"Сочетание цветов: {accents[0]} и {accents[1]}",
            _ => $"Яркий микс: {string.Join(", ", accents)}"
        });

        var fresh = items.Where(x => x.LastWornAt is null || (nowUtc - x.LastWornAt.Value).TotalDays >= 14).ToList();
        if (fresh.Count > 0 && fresh.Count < items.Count)
            reasons.Add($"Давно не надевали: {string.Join(", ", fresh.Take(2).Select(x => LowerFirst(x.Name)))}");
        else if (fresh.Count == items.Count && items.Any(x => x.WearCount > 0))
            reasons.Add("Все вещи давно не надевали");

        var favorite = items.FirstOrDefault(x => x.IsFavorite);
        if (favorite is not null)
            reasons.Add($"Любимая вещь: {LowerFirst(favorite.Name)}");

        return reasons;
    }

    private static string LowerFirst(string value)
    {
        if (value.Length < 2 || !char.IsUpper(value[0]) || char.IsUpper(value[1]))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
