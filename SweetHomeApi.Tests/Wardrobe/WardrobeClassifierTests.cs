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
    public void NearestColor_MapsPhotoColors(string hex, string expected)
    {
        Assert.True(WardrobeClassifier.TryParseHex(hex, out var rgb));
        Assert.Equal(expected, WardrobeClassifier.NearestColor(rgb));
    }
}
