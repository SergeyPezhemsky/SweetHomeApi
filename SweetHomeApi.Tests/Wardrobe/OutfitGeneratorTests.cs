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
    public void WarmNovemberDay_AllowsSummerTops()
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("tee", WardrobeCategory.TOP, 1, "WHITE", seasons: ["SPRING", "SUMMER"]),
            Item("jeans", WardrobeCategory.BOTTOM, 2, "BLUE"),
            Item("sneakers", WardrobeCategory.SHOES, 2, "WHITE")
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 19
        }, Now);

        Assert.NotEmpty(result.Outfits);
    }

    [Fact]
    public void ColdMarchDay_AllowsWinterSweater()
    {
        var march = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var wardrobe = new List<WardrobeItem>
        {
            Item("sweater", WardrobeCategory.TOP, 4, "GRAY", seasons: ["AUTUMN", "WINTER"]),
            Item("jeans", WardrobeCategory.BOTTOM, 2, "BLUE"),
            Item("boots", WardrobeCategory.SHOES, 4, "BLACK"),
            Item("coat", WardrobeCategory.OUTERWEAR, 4, "BEIGE")
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = 1
        }, march);

        Assert.Contains(result.Outfits.SelectMany(x => x.Items), x => x.Id == "sweater");
    }

    [Theory]
    [InlineData(-12)]
    [InlineData(-25)]
    public void SevereFrost_OnlyVeryWarmOuterwear(int temperature)
    {
        var wardrobe = CasualWardrobe();
        wardrobe.Add(Item("denim-jacket", WardrobeCategory.OUTERWEAR, 3, "BLUE"));

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = temperature,
            Count = 10
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.All(result.Outfits, outfit =>
            Assert.Equal(5, outfit.Items.Single(x => x.Category == WardrobeCategory.OUTERWEAR).Warmth));
    }

    [Fact]
    public void SevereFrost_OnlyWarmShoes()
    {
        var wardrobe = CasualWardrobe();
        wardrobe.Add(Item("loafers", WardrobeCategory.SHOES, 2, "BLACK"));

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = -20,
            Count = 10
        }, Now);

        Assert.All(result.Outfits, outfit =>
            Assert.True(outfit.Items.Single(x => x.Category == WardrobeCategory.SHOES).Warmth >= 4));
    }

    [Fact]
    public void SportInCold_PrefersPufferOverFurOrCoat()
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("hoodie", WardrobeCategory.TOP, 3, "GRAY", ["CASUAL", "SPORT"]),
            Item("joggers", WardrobeCategory.BOTTOM, 3, "BLACK", ["CASUAL", "SPORT"]),
            Item("winter-boots", WardrobeCategory.SHOES, 5, "BLACK", ["CASUAL", "OFFICE"]),
            Item("fur-coat", WardrobeCategory.OUTERWEAR, 5, "BROWN", ["CASUAL", "FORMAL"]),
            Item("wool-coat", WardrobeCategory.OUTERWEAR, 4, "BEIGE", ["CASUAL", "OFFICE", "FORMAL"]),
            Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK", ["CASUAL"])
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.SPORT,
            Temperature = -5,
            Count = 3
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.All(result.Outfits, o => Assert.Contains(o.Items, x => x.Id == "puffer"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void NearZero_NoLightJackets(int temperature)
    {
        var wardrobe = CasualWardrobe();
        wardrobe.Add(Item("denim-jacket", WardrobeCategory.OUTERWEAR, 3, "BLUE"));
        wardrobe.Add(Item("trench", WardrobeCategory.OUTERWEAR, 3, "BEIGE"));

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = temperature,
            Count = 5
        }, Now);

        Assert.All(result.Outfits, o =>
            Assert.True(o.Items.Single(x => x.Category == WardrobeCategory.OUTERWEAR).Warmth >= 4));
    }

    [Theory]
    [InlineData(-20)]
    [InlineData(-5)]
    [InlineData(0)]
    public void SportInFrost_UsesWinterSneakersNotHeels(int temperature)
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("hoodie", WardrobeCategory.TOP, 3, "GRAY", ["CASUAL", "SPORT"]),
            Item("leggings", WardrobeCategory.BOTTOM, 2, "BLACK", ["CASUAL", "SPORT"]),
            Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK", ["CASUAL", "SPORT"]),
            Item("Сапоги зимние на каблуке", WardrobeCategory.SHOES, 5, "BLACK", ["CASUAL", "OFFICE"], ["WINTER"]),
            Item("Угги", WardrobeCategory.SHOES, 5, "BEIGE", ["CASUAL"], ["WINTER"]),
            Item("Зимние кроссовки", WardrobeCategory.SHOES, 3, "BLACK", ["CASUAL", "SPORT"], ["WINTER"])
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.SPORT,
            Temperature = temperature,
            Count = 3
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.All(result.Outfits, o => Assert.DoesNotContain(o.Items, x => x.Name.Contains("каблук")));
        Assert.Contains(result.Outfits[0].Items, x => x.Name == "Зимние кроссовки");
    }

    [Fact]
    public void Tie_IsNeverAddedToDress()
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("Платье летнее", WardrobeCategory.DRESS, 1, "WHITE", ["OFFICE"]),
            Item("Лоферы", WardrobeCategory.SHOES, 2, "BLACK", ["OFFICE"]),
            Item("Галстук шёлковый", WardrobeCategory.ACCESSORY, 1, "BLACK", ["OFFICE"])
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.OFFICE,
            Temperature = 28
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.DoesNotContain(result.Outfits.SelectMany(x => x.Items), x => x.Name.StartsWith("Галстук"));
    }

    [Fact]
    public void LargeWardrobe_IsFastEnough()
    {
        var categories = Enum.GetValues<WardrobeCategory>();
        var colors = new[] { "BLACK", "WHITE", "GRAY", "BLUE", "BEIGE", "RED", "GREEN" };
        var wardrobe = Enumerable.Range(0, 300)
            .Select(i => Item($"item-{i}", categories[i % categories.Length], 1 + i % 5, colors[i % colors.Length]))
            .ToList();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = -5,
            Count = 10
        }, Now);
        watch.Stop();

        Assert.NotEmpty(result.Outfits);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData(-20)]
    [InlineData(5)]
    [InlineData(30)]
    public void Home_IgnoresOutdoorWeather(int temperature)
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("home-tee", WardrobeCategory.TOP, 2, "GRAY", ["HOME"]),
            Item("home-pants", WardrobeCategory.BOTTOM, 2, "GRAY", ["HOME"]),
            Item("slippers", WardrobeCategory.SHOES, 1, "BEIGE", ["HOME"]),
            Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK", ["CASUAL"])
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.HOME,
            Temperature = temperature
        }, Now);

        Assert.Empty(result.Missing);
        var outfit = Assert.Single(result.Outfits);
        Assert.DoesNotContain(outfit.Items, x => x.Category == WardrobeCategory.OUTERWEAR);
    }

    [Fact]
    public void OfficeInWinter_FallsBackToCasualStreetwear()
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("shirt", WardrobeCategory.TOP, 2, "WHITE", ["OFFICE"]),
            Item("sweater", WardrobeCategory.TOP, 4, "GRAY", ["OFFICE", "CASUAL"]),
            Item("trousers", WardrobeCategory.BOTTOM, 3, "BLACK", ["OFFICE"]),
            Item("loafers", WardrobeCategory.SHOES, 2, "BLACK", ["OFFICE"], ["SPRING", "SUMMER", "AUTUMN"]),
            Item("winter-boots", WardrobeCategory.SHOES, 5, "BLACK", ["CASUAL"], ["WINTER"]),
            Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK", ["CASUAL"], ["WINTER"])
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.OFFICE,
            Temperature = -15
        }, Now);

        Assert.NotEmpty(result.Outfits);
        Assert.All(result.Outfits, outfit =>
        {
            Assert.Contains(outfit.Items, x => x.Id == "winter-boots");
            Assert.Contains(outfit.Items, x => x.Id == "puffer");
        });
    }

    [Fact]
    public void OfficeInFrost_GetsCasualHatWhenNoOfficeHat()
    {
        var wardrobe = new List<WardrobeItem>
        {
            Item("blouse", WardrobeCategory.TOP, 3, "WHITE", ["OFFICE"]),
            Item("trousers", WardrobeCategory.BOTTOM, 3, "BLACK", ["OFFICE"]),
            Item("boots", WardrobeCategory.SHOES, 5, "BLACK", ["CASUAL", "OFFICE"]),
            Item("puffer", WardrobeCategory.OUTERWEAR, 5, "BLACK", ["CASUAL"]),
            Item("Шарф", WardrobeCategory.ACCESSORY, 4, "GRAY", ["CASUAL", "OFFICE"]),
            Item("Шапка", WardrobeCategory.ACCESSORY, 4, "GRAY", ["CASUAL"])
        };

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.OFFICE,
            Temperature = -20
        }, Now);

        var outfit = Assert.Single(result.Outfits);
        Assert.Contains(outfit.Items, x => x.Name == "Шапка");
        Assert.Contains(outfit.Items, x => x.Name == "Шарф");
    }

    [Fact]
    public void Frost_GetsHatAndScarf()
    {
        var wardrobe = CasualWardrobe();
        wardrobe.Add(Item("Шапка", WardrobeCategory.ACCESSORY, 4, "GRAY"));
        wardrobe.Add(Item("Перчатки", WardrobeCategory.ACCESSORY, 4, "BLACK"));

        var result = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest
        {
            Occasion = WardrobeStyle.CASUAL,
            Temperature = -20,
            Count = 3
        }, Now);

        Assert.All(result.Outfits, outfit => Assert.Contains(outfit.Items, x => x.Name == "Шапка"));
    }

    [Fact]
    public void MildAndCoolDays_GetDifferentOutfits()
    {
        var wardrobe = CasualWardrobe();
        var cool = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest { Occasion = WardrobeStyle.CASUAL, Temperature = 10 }, Now);
        var mild = OutfitGenerator.Generate(wardrobe, new OutfitSuggestionRequest { Occasion = WardrobeStyle.CASUAL, Temperature = 20 }, Now);

        string Key(OutfitSuggestionResult r) => string.Join(";", r.Outfits.Select(o => string.Join("|", o.Items.Select(i => i.Id))));
        Assert.NotEqual(Key(cool), Key(mild));
        Assert.True(mild.Outfits.Count(o => o.Items.Any(i => i.Category == WardrobeCategory.OUTERWEAR)) < mild.Outfits.Count);
        Assert.All(mild.Outfits, o => Assert.DoesNotContain(o.Items, i => i.Warmth >= 4));
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
            var accessories = outfit.Items.Where(x => x.Category == WardrobeCategory.ACCESSORY).ToList();
            Assert.True(accessories.Count <= 3);
            Assert.Equal(accessories.Count, accessories.Select(x => WardrobeClassifier.AccessoryKind(x.Name)).Distinct().Count());
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
        Assert.Equal(OutfitGenerator.TypicalTemperature(Now), result.Temperature);
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
