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

    private const int CandidatesPerSlot = 12;
    private const int MaxCandidates = 50_000;
    private const int ReusePenalty = 8;

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
        var temperature = request.Temperature ?? TypicalTemperature(season);
        var targetWarmth = TargetWarmth(temperature);
        var outerwearMode = temperature < OuterwearRequiredBelow
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
        var seasonCode = season.ToString();
        var suitable = wardrobe
            .Where(x => x.Styles.Contains(occasion))
            .Where(x => x.Seasons.Count == 0 || x.Seasons.Contains(seasonCode))
            .ToList();

        var baseWarmth = Math.Min(targetWarmth, 4);
        List<WardrobeItem> Slot(WardrobeCategory category, Func<WardrobeItem, bool> fits, int desired) => suitable
            .Where(x => x.Category == category && fits(x))
            .OrderByDescending(x => ItemScore(x, desired, nowUtc, seed))
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Take(CandidatesPerSlot)
            .ToList();

        bool BaseFits(WardrobeItem x) => Math.Abs(x.Warmth - baseWarmth) <= 2 && !(targetWarmth <= 2 && x.Warmth >= 4);

        var tops = Slot(WardrobeCategory.TOP, BaseFits, baseWarmth);
        var bottoms = Slot(WardrobeCategory.BOTTOM, BaseFits, baseWarmth);
        var dresses = Slot(WardrobeCategory.DRESS, BaseFits, baseWarmth);
        var shoes = Slot(WardrobeCategory.SHOES, x => Math.Abs(x.Warmth - targetWarmth) <= 2, targetWarmth);
        var outerwear = outerwearMode == OuterwearMode.Forbidden
            ? []
            : Slot(WardrobeCategory.OUTERWEAR, x => x.Warmth >= targetWarmth - 1 && x.Warmth <= targetWarmth + 1, targetWarmth);
        var accessories = Slot(WardrobeCategory.ACCESSORY, x => Math.Abs(x.Warmth - targetWarmth) <= 2, targetWarmth);

        FillMissing(result, request.Occasion, season, targetWarmth, outerwearMode, tops, bottoms, dresses, shoes, outerwear);
        if (result.Missing.Count > 0)
            return result;

        var outerOptions = outerwearMode switch
        {
            OuterwearMode.Required => outerwear.Select(x => (WardrobeItem?)x).ToList(),
            OuterwearMode.Forbidden => [null],
            _ => outerwear.Select(x => (WardrobeItem?)x).Prepend(null).ToList()
        };

        var bases = new List<List<WardrobeItem>>();
        foreach (var top in tops)
        foreach (var bottom in bottoms)
            bases.Add([top, bottom]);
        foreach (var dress in dresses)
            bases.Add([dress]);

        var candidates = new List<Candidate>();
        foreach (var baseItems in bases)
        foreach (var shoe in shoes)
        foreach (var outer in outerOptions)
        {
            if (candidates.Count >= MaxCandidates)
                break;

            var items = new List<WardrobeItem>(baseItems) { shoe };
            if (outer is not null)
                items.Add(outer);

            var score = OutfitScore(items, baseWarmth, targetWarmth, nowUtc, seed);

            // Аксессуар добавляем, только если он не портит образ.
            WardrobeItem? bestAccessory = null;
            var bestWithAccessory = score;
            foreach (var accessory in accessories)
            {
                var withAccessory = OutfitScore([..items, accessory], baseWarmth, targetWarmth, nowUtc, seed) + 2;
                if (withAccessory > bestWithAccessory)
                {
                    bestWithAccessory = withAccessory;
                    bestAccessory = accessory;
                }
            }

            if (bestAccessory is not null)
            {
                items.Add(bestAccessory);
                score = bestWithAccessory;
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
            .Take(2000)
            .ToList();

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
                Reasons = BuildReasons(best.Items, temperature, temperatureAssumed, nowUtc)
            });
        }

        return result;
    }

    public static int TargetWarmth(int temperature) => temperature switch
    {
        >= 25 => 1,
        >= 18 => 2,
        >= 10 => 3,
        >= 0 => 4,
        _ => 5
    };

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
            var level = WardrobeDictionaries.WarmthLevels.First(x => x.Level == Math.Max(1, targetWarmth - 1)).Name.ToLowerInvariant();
            result.Missing.Add($"Добавьте верхнюю одежду {context} — теплота не ниже «{level}»");
        }
    }

    private static double ItemScore(WardrobeItem item, int desiredWarmth, DateTime nowUtc, int seed)
    {
        return 10 - 3 * Math.Abs(item.Warmth - desiredWarmth)
               + 4 * Freshness(item, nowUtc)
               + (item.IsFavorite ? 2 : 0)
               + Jitter(item.Id, seed);
    }

    private static double OutfitScore(List<WardrobeItem> items, int baseWarmth, int targetWarmth, DateTime nowUtc, int seed)
    {
        // Теплота: 35 баллов.
        var warmthPenalty = items.Sum(x =>
        {
            var desired = x.Category is WardrobeCategory.OUTERWEAR or WardrobeCategory.SHOES or WardrobeCategory.ACCESSORY
                ? targetWarmth
                : baseWarmth;
            return Math.Abs(x.Warmth - desired);
        });
        var warmthScore = Math.Max(0, 35 - 7.0 * warmthPenalty / items.Count * 2);

        // Цвет: 35 баллов.
        var colorScore = ColorHarmony(items);

        // Свежесть: 20 баллов.
        var freshnessScore = 20 * items.Average(x => Freshness(x, nowUtc));

        // Любимые вещи: 10 баллов.
        var favoriteScore = 10.0 * items.Count(x => x.IsFavorite) / items.Count;

        var jitter = items.Sum(x => Jitter(x.Id, seed)) / items.Count * 2;
        return warmthScore + colorScore + freshnessScore + favoriteScore + jitter;
    }

    public static double ColorHarmony(IReadOnlyCollection<WardrobeItem> items)
    {
        var colors = items.SelectMany(x => x.Colors.Take(2)).ToList();
        var accents = colors
            .Where(x => WardrobeDictionaries.FindColor(x) is { Neutral: false })
            .Where(x => x != "MULTI")
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
        ("BURGUNDY", "RED"),
        ("YELLOW", "PURPLE"),
        ("GREEN", "PINK")
    ];

    /// <summary>0 — надевали в последние 3 дня, 1 — давно не надевали или не надевали никогда.</summary>
    private static double Freshness(WardrobeItem item, DateTime nowUtc)
    {
        if (item.LastWornAt is null)
            return 1;

        var days = (nowUtc - item.LastWornAt.Value).TotalDays;
        if (days < 3)
            return 0;
        if (days >= 14)
            return 1;

        return (days - 3) / 11;
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
