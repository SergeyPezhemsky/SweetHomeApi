using Application.Modules.Wardrobe;

namespace SweetHomeApi.Tests.Wardrobe;

public class OutfitGeneratorTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

    private static WardrobeItem Item(
        string id,
        WardrobeCategory category,
        int warmth,
        string color = "BLACK",
        string[]? styles = null,
        string[]? seasons = null,
        DateTime? lastWornAt = null,
        bool favorite = false)
    {
        return new WardrobeItem
        {
            Id = id,
            Name = id,
            Category = category,
            Warmth = warmth,
            Colors = [color],
            Styles = (styles ?? ["CASUAL"]).ToList(),
            Seasons = (seasons ?? []).ToList(),
            LastWornAt = lastWornAt,
            IsFavorite = favorite,
            CreatedAt = Now.AddDays(-30)
        };
    }

    private static List<WardrobeItem> CasualWardrobe() =>
    [
        Item("tee-white", WardrobeCategory.TOP, 1, "WHITE"),
        Item("shirt-blue", WardrobeCategory.TOP, 2, "LIGHT_BLUE"),
        Item("sweater-gray", WardrobeCategory.TOP, 4, "GRAY"),
        Item("hoodie-green", WardrobeCategory.TOP, 3, "GREEN"),
        Item("jeans", WardrobeCategory.BOTTOM, 2, "BLUE"),
        Item("shorts", WardrobeCategory.BOTTOM, 1, "BEIGE", seasons: ["SUMMER"]),
        Item("chinos", WardrobeCategory.BOTTOM, 2, "BEIGE"),
        Item("dress-red", WardrobeCategory.DRESS, 2, "RED"),
        Item("sneakers", WardrobeCategory.SHOES, 2, "WHITE"),
        Item("sandals", WardrobeCategory.SHOES, 1, "BROWN", seasons: ["SUMMER"]),
        Item("boots", WardrobeCategory.SHOES, 4, "BLACK"),
        Item("winter-boots", WardrobeCategory.SHOES, 5, "BLACK", seasons: ["WINTER"]),
        Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK", seasons: ["WINTER"]),
        Item("coat", WardrobeCategory.OUTERWEAR, 4, "BEIGE"),
        Item("windbreaker", WardrobeCategory.OUTERWEAR, 2, "YELLOW"),
        Item("scarf", WardrobeCategory.ACCESSORY, 4, "GRAY"),
        Item("cap", WardrobeCategory.ACCESSORY, 1, "BLACK")
    ];

    [Fact]
    public void ColdWeather_EveryOutfitHasExactlyOneOuterwear()
    {
        var result = OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = -5,
            Count = 5
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.Equal(5, result.TargetWarmth);
        Assert.Equal(WardrobeSeason.WINTER, result.Season);
        Assert.All(result.Outfits, outfit =>
        {
            Assert.Single(outfit.Items, x => x.Category == WardrobeCategory.OUTERWEAR);
            Assert.All(outfit.Items.Where(x => x.Category == WardrobeCategory.OUTERWEAR), x => Assert.True(x.Warmth >= 4));
            Assert.DoesNotContain(outfit.Items, x => x.Id == "sandals");
        });
    }

    [Fact]
    public void HotWeather_NoOuterwearAndNoWarmPieces()
    {
        var result = OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 28,
            Count = 5
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.All(result.Outfits, outfit =>
        {
            Assert.DoesNotContain(outfit.Items, x => x.Category == WardrobeCategory.OUTERWEAR);
            Assert.DoesNotContain(outfit.Items, x => x.Warmth >= 3);
        });
    }

    [Fact]
    public void Outfits_AreReturnedBestFirst()
    {
        var result = OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = -5,
            Count = 10
        }, Now);

        Assert.Equal(result.Outfits.Select(x => x.Score).OrderByDescending(x => x), result.Outfits.Select(x => x.Score));
    }

    [Fact]
    public void TooWarmOuterwearOnly_HintNamesTheRange()
    {
        var wardrobe = CasualWardrobe().Where(x => x.Category != WardrobeCategory.OUTERWEAR).ToList();
        wardrobe.Add(Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK"));

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 12
        }, Now);

        Assert.Contains(result.Missing, x => x.Contains("от «лёгкая» до «тёплая»"));
    }

    [Fact]
    public void OccasionFilter_OnlyUsesItemsOfThatStyle()
    {
        var wardrobe = CasualWardrobe();
        wardrobe.AddRange(
        [
            Item("office-shirt", WardrobeCategory.TOP, 2, "WHITE", ["OFFICE"]),
            Item("office-trousers", WardrobeCategory.BOTTOM, 2, "NAVY", ["OFFICE", "FORMAL"]),
            Item("loafers", WardrobeCategory.SHOES, 2, "BROWN", ["OFFICE", "CASUAL"]),
            Item("blazer", WardrobeCategory.OUTERWEAR, 2, "NAVY", ["OFFICE"])
        ]);

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.OFFICE,
            Temperature = 20,
            Count = 3
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.All(result.Outfits.SelectMany(x => x.Items), item => Assert.Contains("OFFICE", item.Styles));
    }

    [Theory]
    [InlineData(-15)]
    [InlineData(5)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(30)]
    public void Outfits_HaveValidStructureAndDistinctItemSets(int temperature)
    {
        var result = OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = temperature,
            Count = 10
        }, Now);

        Assert.NotEmpty(result.Outfits);
        var keys = new HashSet<string>();
        foreach (var outfit in result.Outfits)
        {
            var byCategory = outfit.Items.GroupBy(x => x.Category).ToDictionary(x => x.Key, x => x.Count());
            var classic = byCategory.GetValueOrDefault(WardrobeCategory.TOP) == 1
                          && byCategory.GetValueOrDefault(WardrobeCategory.BOTTOM) == 1
                          && !byCategory.ContainsKey(WardrobeCategory.DRESS);
            var dress = byCategory.GetValueOrDefault(WardrobeCategory.DRESS) == 1
                        && !byCategory.ContainsKey(WardrobeCategory.TOP)
                        && !byCategory.ContainsKey(WardrobeCategory.BOTTOM);

            Assert.True(classic || dress, string.Join(",", outfit.Items.Select(x => x.Id)));
            Assert.Equal(1, byCategory.GetValueOrDefault(WardrobeCategory.SHOES));
            Assert.True(byCategory.GetValueOrDefault(WardrobeCategory.OUTERWEAR) <= 1);
            Assert.True(byCategory.GetValueOrDefault(WardrobeCategory.ACCESSORY) <= 1);
            Assert.InRange(outfit.Score, 0, 100);
            Assert.NotEmpty(outfit.Reasons);
            Assert.True(keys.Add(string.Join("|", outfit.Items.Select(x => x.Id).Order())));
        }
    }

    [Fact]
    public void NoShoes_ReturnsMissingHint()
    {
        var wardrobe = CasualWardrobe().Where(x => x.Category != WardrobeCategory.SHOES).ToList();

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 18
        }, Now);

        Assert.Empty(result.Outfits);
        Assert.Contains(result.Missing, x => x.Contains("обувь"));
    }

    [Fact]
    public void ColdWithoutWarmOuterwear_ReturnsMissingHint()
    {
        var wardrobe = CasualWardrobe().Where(x => x.Category != WardrobeCategory.OUTERWEAR).ToList();

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = -10
        }, Now);

        Assert.Empty(result.Outfits);
        Assert.Contains(result.Missing, x => x.Contains("верхнюю одежду"));
    }

    [Fact]
    public void EmptyWardrobe_ExplainsWhatToAdd()
    {
        var result = OutfitGenerator.Generate([], new OutfitSuggestionRequest { Occasion = WardrobeStyle.FORMAL }, Now);

        Assert.Empty(result.Outfits);
        Assert.True(result.Missing.Count >= 2);
        Assert.All(result.Missing, x => Assert.Contains("Торжественный", x));
    }

    [Fact]
    public void SameSeed_IsDeterministic_DifferentSeedVaries()
    {
        var request = new OutfitSuggestionRequest { Occasion = WardrobeStyle.CASUAL, Temperature = 12, Count = 3, Seed = 42 };

        var first = OutfitGenerator.Generate(CasualWardrobe(), request, Now);
        var second = OutfitGenerator.Generate(CasualWardrobe(), request, Now);

        Assert.Equal(
            first.Outfits.Select(x => string.Join("|", x.Items.Select(i => i.Id))),
            second.Outfits.Select(x => string.Join("|", x.Items.Select(i => i.Id))));

        var variants = Enumerable.Range(1, 8)
            .Select(seed => OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest
            {
                Occasion = WardrobeStyle.CASUAL,
                Temperature = 12,
                Count = 1,
                Seed = seed
            }, Now))
            .Select(x => string.Join("|", x.Outfits[0].Items.Select(i => i.Id)))
            .Distinct()
            .Count();

        Assert.True(variants > 1, "Разные seed должны давать разные образы");
    }

    [Fact]
    public void RecentlyWornItems_ArePickedLess()
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("top-a", WardrobeCategory.TOP, 2, "WHITE", lastWornAt: Now.AddDays(-1)),
            Item("top-b", WardrobeCategory.TOP, 2, "WHITE"),
            Item("jeans", WardrobeCategory.BOTTOM, 2, "BLUE"),
            Item("sneakers", WardrobeCategory.SHOES, 2, "WHITE")
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 20,
            Count = 1
        }, Now);

        Assert.Contains(result.Outfits[0].Items, x => x.Id == "top-b");
    }

    [Fact]
    public void Diversity_AvoidsReusingTheSameItemsWhenPossible()
    {
        var result = OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 18,
            Count = 3
        }, Now);

        var tops = result.Outfits.Select(x => x.Items.FirstOrDefault(i => i.Category is WardrobeCategory.TOP or WardrobeCategory.DRESS)?.Id).ToList();
        Assert.Equal(tops.Count, tops.Distinct().Count());
    }

    [Fact]
    public void NoTemperature_UsesSeasonFromDate()
    {
        var result = OutfitGenerator.Generate(CasualWardrobe(), new OutfitSuggestionRequest { Occasion = WardrobeStyle.CASUAL }, Now);

        Assert.Equal(WardrobeSeason.AUTUMN, result.Season);
        Assert.True(result.TemperatureAssumed);
        Assert.Equal(OutfitGenerator.TypicalTemperature(WardrobeSeason.AUTUMN), result.Temperature);
    }

    [Fact]
    public void ColorHarmony_PrefersFewAccents()
    {
        var calm = new[] { Item("a", WardrobeCategory.TOP, 2, "WHITE"), Item("b", WardrobeCategory.BOTTOM, 2, "RED") };
        var loud = new[]
        {
            Item("a", WardrobeCategory.TOP, 2, "PINK"), Item("b", WardrobeCategory.BOTTOM, 2, "RED"),
            Item("c", WardrobeCategory.SHOES, 2, "ORANGE")
        };

        Assert.True(OutfitGenerator.ColorHarmony(calm) > OutfitGenerator.ColorHarmony(loud));
    }
}
