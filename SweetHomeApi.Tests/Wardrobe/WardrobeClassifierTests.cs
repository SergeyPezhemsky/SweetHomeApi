using Application.Modules.Wardrobe;

namespace SweetHomeApi.Tests.Wardrobe;

public class WardrobeClassifierTests
{
    [Fact]
    public void WarmWoolSweater_IsWarmWinterGrayTop()
    {
        var result = WardrobeClassifier.Classify("Тёплый шерстяной свитер серый");

        Assert.Equal(WardrobeCategory.TOP, result.Category);
        Assert.True(result.Warmth >= 4);
        Assert.Contains("WINTER", result.Seasons);
        Assert.Contains("GRAY", result.Colors);
        Assert.True(result.Confidence > 0.6);
    }

    [Fact]
    public void WhiteSneakers_AreCasualOrSportWhiteShoes()
    {
        var result = WardrobeClassifier.Classify("White sneakers");

        Assert.Equal(WardrobeCategory.SHOES, result.Category);
        Assert.True(result.Styles.Contains("CASUAL") || result.Styles.Contains("SPORT"));
        Assert.Contains("WHITE", result.Colors);
    }

    [Fact]
    public void ColorFromPhoto_IsUsedWhenTextHasNoColor()
    {
        var result = WardrobeClassifier.Classify("Пальто", colorHex: "#1F2A44");

        Assert.Equal(WardrobeCategory.OUTERWEAR, result.Category);
        Assert.Contains("NAVY", result.Colors);
    }

    [Fact]
    public void TextColorWinsOverPhotoColor()
    {
        var result = WardrobeClassifier.Classify("Красное платье", colorHex: "#1F2A44");

        Assert.Equal(WardrobeCategory.DRESS, result.Category);
        Assert.Equal(["RED"], result.Colors);
    }

    [Fact]
    public void UnknownText_HasNoCategoryAndZeroConfidence()
    {
        var result = WardrobeClassifier.Classify("Штука");

        Assert.Null(result.Category);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void EmptyText_DoesNotThrow()
    {
        var result = WardrobeClassifier.Classify(null, null, "not-a-color");

        Assert.Null(result.Category);
        Assert.Empty(result.Colors);
    }

    [Theory]
    [InlineData("Джинсовая куртка", WardrobeCategory.OUTERWEAR, "BLUE")]
    [InlineData("Джинсы мом", WardrobeCategory.BOTTOM, "BLUE")]
    [InlineData("Тёмно-синие брюки", WardrobeCategory.BOTTOM, "NAVY")]
    [InlineData("Чёрные кожаные ботинки челси", WardrobeCategory.SHOES, "BLACK")]
    [InlineData("Бежевый тренч", WardrobeCategory.OUTERWEAR, "BEIGE")]
    [InlineData("Белая футболка оверсайз", WardrobeCategory.TOP, "WHITE")]
    [InlineData("Зимний пуховик", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Шерстяной шарф в клетку", WardrobeCategory.ACCESSORY, "MULTI")]
    [InlineData("Black leather boots", WardrobeCategory.SHOES, "BLACK")]
    [InlineData("Grey hoodie", WardrobeCategory.TOP, "GRAY")]
    [InlineData("Юбка миди оливковая", WardrobeCategory.BOTTOM, "OLIVE")]
    [InlineData("Серьги серебряные", WardrobeCategory.ACCESSORY, "GRAY")]
    [InlineData("Baggy jeans", WardrobeCategory.BOTTOM, "BLUE")]
    [InlineData("Bootcut jeans", WardrobeCategory.BOTTOM, null)]
    [InlineData("Capri pants", WardrobeCategory.BOTTOM, null)]
    [InlineData("Tie-dye t-shirt", WardrobeCategory.TOP, "MULTI")]
    [InlineData("Dress shirt white", WardrobeCategory.TOP, "WHITE")]
    [InlineData("Dress shoes black", WardrobeCategory.SHOES, "BLACK")]
    [InlineData("Shirt dress", WardrobeCategory.DRESS, null)]
    [InlineData("Sweater dress", WardrobeCategory.DRESS, null)]
    [InlineData("Suit jacket", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Running shoes", WardrobeCategory.SHOES, null)]
    [InlineData("Grey sweatpants", WardrobeCategory.BOTTOM, "GRAY")]
    [InlineData("Navy peacoat", WardrobeCategory.OUTERWEAR, "NAVY")]
    [InlineData("Leather handbag", WardrobeCategory.ACCESSORY, null)]
    [InlineData("Сапожки замшевые", WardrobeCategory.SHOES, null)]
    [InlineData("Ботильоны на каблуке", WardrobeCategory.SHOES, null)]
    [InlineData("Сумочка через плечо", WardrobeCategory.ACCESSORY, null)]
    [InlineData("Курточка лёгкая", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Платье-рубашка", WardrobeCategory.DRESS, null)]
    [InlineData("Бирюзовая блузка", WardrobeCategory.TOP, "LIGHT_BLUE")]
    [InlineData("Футболочка белая", WardrobeCategory.TOP, "WHITE")]
    [InlineData("Маечка", WardrobeCategory.TOP, null)]
    [InlineData("Юбочка", WardrobeCategory.BOTTOM, null)]
    [InlineData("Туфельки", WardrobeCategory.SHOES, null)]
    [InlineData("Брючки", WardrobeCategory.BOTTOM, null)]
    [InlineData("Шапочка", WardrobeCategory.ACCESSORY, null)]
    [InlineData("Джинсики", WardrobeCategory.BOTTOM, "BLUE")]
    [InlineData("Джинсовка", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Валенки", WardrobeCategory.SHOES, null)]
    [InlineData("Олимпийка", WardrobeCategory.TOP, null)]
    [InlineData("Слипоны", WardrobeCategory.SHOES, null)]
    [InlineData("Эспадрильи", WardrobeCategory.SHOES, null)]
    [InlineData("Бриджи", WardrobeCategory.BOTTOM, null)]
    [InlineData("Flip flops", WardrobeCategory.SHOES, null)]
    [InlineData("Майкл Корс сумка", WardrobeCategory.ACCESSORY, null)]
    [InlineData("Trench coat", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Пуховичок", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Пальтишко", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Пиджачок", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Рюкзачок", WardrobeCategory.ACCESSORY, null)]
    [InlineData("Кроссовочки", WardrobeCategory.SHOES, null)]
    [InlineData("Кедики", WardrobeCategory.SHOES, null)]
    [InlineData("Кожанка", WardrobeCategory.OUTERWEAR, null)]
    [InlineData("Гольф", WardrobeCategory.TOP, null)]
    [InlineData("Платье бордо", WardrobeCategory.DRESS, "BURGUNDY")]
    [InlineData("Кэмел пальто", WardrobeCategory.OUTERWEAR, "BEIGE")]
    [InlineData("Charcoal trousers", WardrobeCategory.BOTTOM, "GRAY")]
    [InlineData("Lavender blouse", WardrobeCategory.TOP, "PURPLE")]
    [InlineData("Топы", WardrobeCategory.TOP, null)]
    [InlineData("Кроксы", WardrobeCategory.SHOES, null)]
    [InlineData("Броги коричневые", WardrobeCategory.SHOES, "BROWN")]
    [InlineData("Ботфорты", WardrobeCategory.SHOES, null)]
    [InlineData("Мартинсы", WardrobeCategory.SHOES, null)]
    [InlineData("Палантин", WardrobeCategory.ACCESSORY, null)]
    [InlineData("Платье в цветочек", WardrobeCategory.DRESS, "MULTI")]
    [InlineData("Khaki chinos", WardrobeCategory.BOTTOM, "BEIGE")]
    [InlineData("Худи Off-White", WardrobeCategory.TOP, null)]
    public void ClassifiesCommonItems(string name, WardrobeCategory category, string? color)
    {
        var result = WardrobeClassifier.Classify(name);

        Assert.Equal(category, result.Category);
        if (color is not null)
            Assert.Contains(color, result.Colors);
    }

    [Fact]
    public void SuitJacket_IsOfficeBlazerNotWarmCasualJacket()
    {
        var result = WardrobeClassifier.Classify("Suit jacket");

        Assert.Equal(2, result.Warmth);
        Assert.Contains("OFFICE", result.Styles);
    }

    [Fact]
    public void RunningShoes_AreSport()
    {
        Assert.Equal("SPORT", WardrobeClassifier.Classify("Running shoes").Styles[0]);
    }

    [Fact]
    public void LightBlue_IsAColorNotALightFabric()
    {
        var result = WardrobeClassifier.Classify("Light blue shirt");

        Assert.Equal(2, result.Warmth);
        Assert.Equal(["LIGHT_BLUE"], result.Colors);
    }

    [Fact]
    public void Earrings_AreNotGray()
    {
        var result = WardrobeClassifier.Classify("Серёжки золотые");

        Assert.Equal(WardrobeCategory.ACCESSORY, result.Category);
        Assert.DoesNotContain("GRAY", result.Colors);
    }

    [Fact]
    public void DownJackets_AreVeryWarmWinter()
    {
        Assert.Equal(5, WardrobeClassifier.Classify("Пуховая куртка").Warmth);
        Assert.Equal(5, WardrobeClassifier.Classify("Puffer jacket").Warmth);
        Assert.Equal(3, WardrobeClassifier.Classify("Trench coat").Warmth);
        Assert.Equal(3, WardrobeClassifier.Classify("Light grey hoodie").Warmth);
    }

    [Theory]
    [InlineData("Джинсовая куртка")]
    [InlineData("Кожаная куртка")]
    [InlineData("Fleece jacket")]
    [InlineData("Denim jacket")]
    [InlineData("Жилетка флисовая")]
    public void LightJackets_AreDemiSeasonNotWinter(string name)
    {
        var result = WardrobeClassifier.Classify(name);

        Assert.Equal(WardrobeCategory.OUTERWEAR, result.Category);
        Assert.True(result.Warmth <= 3, $"{name}: warmth {result.Warmth}");
        Assert.DoesNotContain("WINTER", result.Seasons);
    }

    [Theory]
    [InlineData("White sneakers", "подарок, к белой футболке", WardrobeCategory.SHOES)]
    [InlineData("Linen shirt", "Ношу с брюками", WardrobeCategory.TOP)]
    public void Name_WinsOverNotes(string name, string notes, WardrobeCategory expected)
    {
        Assert.Equal(expected, WardrobeClassifier.Classify(name, notes).Category);
    }

    [Fact]
    public void ColorsInNotes_AreNotTaken()
    {
        Assert.Equal(["BLACK"], WardrobeClassifier.Classify("Чёрные брюки", "носить с белой рубашкой").Colors);
    }

    [Theory]
    [InlineData("Levi's 501", "к белой футболке")]
    [InlineData("Mango", "под пальто")]
    public void Notes_NeverDecideCategory(string name, string notes)
    {
        Assert.Null(WardrobeClassifier.Classify(name, notes).Category);
    }

    [Fact]
    public void Notes_DoNotChangeWarmthOrSeasons()
    {
        var result = WardrobeClassifier.Classify("Футболка", "ношу под тёплый свитер зимой");

        Assert.Equal(1, result.Warmth);
        Assert.DoesNotContain("WINTER", result.Seasons);
    }

    [Theory]
    [InlineData("Резиновые сапоги", 3)]
    [InlineData("Rain jacket", 3)]
    [InlineData("Демисезонные сапоги", 4)]
    public void RainAndDemiSeasonItems_AreNotWinterWarm(string name, int maxWarmth)
    {
        var result = WardrobeClassifier.Classify(name);

        Assert.True(result.Warmth <= maxWarmth, $"{name}: {result.Warmth}");
        Assert.DoesNotContain("WINTER", result.Seasons);
    }

    [Theory]
    [InlineData("Шапка вязаная", "head")]
    [InlineData("Шарф", "neck")]
    [InlineData("Перчатки кожаные", "hands")]
    [InlineData("Рюкзак", "bag")]
    [InlineData("Часы", "other")]
    public void AccessoryKinds(string name, string kind)
    {
        Assert.Equal(kind, WardrobeClassifier.AccessoryKind(name));
    }

    [Fact]
    public void NavyBlue_IsOneColor()
    {
        Assert.Equal(["NAVY"], WardrobeClassifier.Classify("Navy blue blazer").Colors);
    }

    [Fact]
    public void SilkTie_IsAllSeason()
    {
        Assert.Empty(WardrobeClassifier.Classify("Шёлковый галстук").Seasons);
    }

    [Fact]
    public void InsulatedTrousers_AreColdSeason()
    {
        Assert.DoesNotContain("SUMMER", WardrobeClassifier.Classify("Утеплённые брюки").Seasons);
        Assert.NotEmpty(WardrobeClassifier.Classify("Утеплённые брюки").Seasons);
    }

    [Fact]
    public void CargoPants_AreCasual()
    {
        Assert.Equal("CASUAL", WardrobeClassifier.Classify("Брюки карго").Styles[0]);
    }

    [Fact]
    public void SweaterDress_IsWarm()
    {
        Assert.True(WardrobeClassifier.Classify("Платье-свитер").Warmth >= 3);
    }

    [Fact]
    public void DarkBlue_IsNotAlsoBlue()
    {
        var result = WardrobeClassifier.Classify("Тёмно-синий пиджак");

        Assert.Equal(["NAVY"], result.Colors);
    }

    [Fact]
    public void SyntheticFabric_IsNotBlue()
    {
        var result = WardrobeClassifier.Classify("Синтетическая рубашка");

        Assert.DoesNotContain("BLUE", result.Colors);
    }

    [Fact]
    public void DownJacket_IsVeryWarmWinterOuterwear()
    {
        var result = WardrobeClassifier.Classify("Зимний пуховик");

        Assert.Equal(5, result.Warmth);
        Assert.Equal(["WINTER"], result.Seasons);
    }

    [Fact]
    public void SportModifier_PutsSportStyleFirst()
    {
        var result = WardrobeClassifier.Classify("Спортивные легинсы");

        Assert.Equal(WardrobeCategory.BOTTOM, result.Category);
        Assert.Equal("SPORT", result.Styles[0]);
    }

    [Theory]
    [InlineData("#000000", "BLACK")]
    [InlineData("#FFFFFF", "WHITE")]
    [InlineData("#808080", "GRAY")]
    [InlineData("#D02020", "RED")]
    [InlineData("#2060E0", "BLUE")]
    [InlineData("#F5DC3C", "YELLOW")]
    [InlineData("#3A8F48", "GREEN")]
    [InlineData("#D9C2A3", "BEIGE")]
    [InlineData("#C19A6B", "BEIGE")]
    [InlineData("#5D7B9D", "BLUE")]
    [InlineData("#3E5F8A", "BLUE")]
    [InlineData("#800080", "PURPLE")]
    [InlineData("#98FF98", "GREEN")]
    [InlineData("#E1AD01", "YELLOW")]
    [InlineData("#36454F", "GRAY")]
    [InlineData("#C3B091", "BEIGE")]
    [InlineData("#D8BFD8", "PURPLE")]
    [InlineData("#D3D3D3", "GRAY")]
    [InlineData("#C2B280", "BEIGE")]
    [InlineData("#4B5320", "OLIVE")]
    [InlineData("#614051", "PURPLE")]
    [InlineData("#FDFD96", "YELLOW")]
    [InlineData("#9CAF88", "GREEN")]
    [InlineData("#141B2D", "NAVY")]
    [InlineData("#3E2723", "BROWN")]
    [InlineData("#C8A2C8", "PURPLE")]
    [InlineData("#E3D9C6", "BEIGE")]
    public void NearestColor_MapsPhotoColors(string hex, string expected)
    {
        Assert.True(WardrobeClassifier.TryParseHex(hex, out var rgb));
        Assert.Equal(expected, WardrobeClassifier.NearestColor(rgb));
    }
}
