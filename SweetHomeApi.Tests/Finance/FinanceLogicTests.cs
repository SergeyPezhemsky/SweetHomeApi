using Application.Modules.Finance;
using SweetHomeApi.Infrastructure.Finance;

namespace SweetHomeApi.Tests.Finance;

public class FinanceLogicTests
{
    [Fact]
    public void AllocationScalesItemsToReceiptTotal()
    {
        // Сценарий из спецификации: итог 900, позиции 600 + 400.
        var result = FinanceAllocation.Allocate(900m, FinanceCategory.OTHER,
        [
            (FinanceCategory.GROCERIES, 600m),
            (FinanceCategory.HOME, 400m)
        ]);

        Assert.Equal(540m, result[FinanceCategory.GROCERIES]);
        Assert.Equal(360m, result[FinanceCategory.HOME]);
    }

    [Fact]
    public void AllocationAlwaysAddsUpToTotalToTheKopeck()
    {
        var result = FinanceAllocation.Allocate(100m, FinanceCategory.OTHER,
        [
            (FinanceCategory.GROCERIES, 1m),
            (FinanceCategory.HOME, 1m),
            (FinanceCategory.PETS, 1m)
        ]);

        Assert.Equal(100m, result.Values.Sum());
        Assert.All(result.Values, x => Assert.InRange(x, 33.33m, 33.34m));
    }

    [Fact]
    public void DiscountLineDoesNotGetNegativeShare()
    {
        var result = FinanceAllocation.Allocate(450m, FinanceCategory.OTHER,
        [
            (FinanceCategory.GROCERIES, 300m),
            (FinanceCategory.HOME, 200m),
            (FinanceCategory.OTHER, -50m)
        ]);

        Assert.Equal(270m, result[FinanceCategory.GROCERIES]);
        Assert.Equal(180m, result[FinanceCategory.HOME]);
        Assert.False(result.ContainsKey(FinanceCategory.OTHER));
    }

    [Fact]
    public void ReceiptWithoutItemsGoesToReceiptCategory()
    {
        var result = FinanceAllocation.Allocate(1234.5m, FinanceCategory.CAFE, []);

        Assert.Equal(1234.5m, Assert.Single(result).Value);
        Assert.Equal(FinanceCategory.CAFE, result.Keys.Single());
        Assert.Empty(FinanceAllocation.Allocate(0m, FinanceCategory.CAFE, [(FinanceCategory.HOME, 10m)]));
    }

    [Theory]
    [InlineData("Корм Purina Pro Plan 1,5 кг", "корм purina")]
    [InlineData("КОРМ PURINA для щенков 3кг", "корм purina")]
    [InlineData("Молоко 2,5% 930мл", "молоко")]
    [InlineData("Ёлочная игрушка", "елочная игрушка")]
    [InlineData("123 456", null)]
    [InlineData("  ", null)]
    public void ItemKeyUsesTwoMeaningfulWords(string name, string? expected)
    {
        Assert.Equal(expected, FinanceItemKey.Of(name));
    }

    [Fact]
    public void ParserReadsFencedJsonWithStringNumbers()
    {
        const string response = """
            Вот результат:
            ```json
            {
              "isReceipt": true,
              "store": "ВкусВилл",
              "date": "14.11.2026 18:32",
              "currency": "₽",
              "total": "1 234,50",
              "category": "groceries",
              "items": [
                {"name": "Молоко 2,5%", "quantity": 2, "price": "89,90", "amount": 179.8, "category": "GROCERIES"},
                {"name": "Губки для посуды", "amount": "54,70", "category": "HOME"},
                {"name": "Неизвестное", "amount": 1000, "category": "SPACESHIPS"},
                {"name": "", "amount": 5},
                {"name": "Ноль", "amount": 0}
              ]
            }
            ```
            """;

        var parsed = ReceiptRecognitionParser.Parse(response);

        Assert.NotNull(parsed);
        Assert.True(parsed!.IsReceipt);
        Assert.Equal("ВкусВилл", parsed.Store);
        Assert.Equal(new DateOnly(2026, 11, 14), parsed.Date);
        Assert.Equal("RUB", parsed.Currency);
        Assert.Equal(1234.50m, parsed.Total);
        Assert.Equal(FinanceCategory.GROCERIES, parsed.Category);
        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal(89.90m, parsed.Items[0].Price);
        Assert.Equal(2m, parsed.Items[0].Quantity);
        Assert.Equal(54.70m, parsed.Items[1].Amount);
        Assert.Equal(FinanceCategory.OTHER, parsed.Items[2].Category);
    }

    [Theory]
    [InlineData("""{"isReceipt": false}""")]
    [InlineData("""{"isReceipt": "false", "total": 10}""")]
    public void ParserRecognizesNotAReceipt(string response)
    {
        Assert.False(ReceiptRecognitionParser.Parse(response)!.IsReceipt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Не могу прочитать чек")]
    [InlineData("{ broken json")]
    [InlineData("[1, 2, 3]")]
    public void ParserReturnsNullWithoutJsonObject(string? response)
    {
        Assert.Null(ReceiptRecognitionParser.Parse(response));
    }

    [Fact]
    public void ParserSkipsBracesInsideStringsAndInvalidPrefixes()
    {
        var parsed = ReceiptRecognitionParser.Parse("""пример {не json} и ответ {"store": "Кафе {Ромашка}", "total": 300}""");

        Assert.Equal("Кафе {Ромашка}", parsed!.Store);
        Assert.Equal(300m, parsed.Total);
    }

    [Theory]
    [InlineData("1 234,50", 1234.50)]
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("-50,00 ₽", -50.0)]
    [InlineData("abc", null)]
    public void AmountParsing(string text, double? expected)
    {
        Assert.Equal(expected is null ? null : (decimal)expected.Value, ReceiptRecognitionParser.ParseAmount(text));
    }

    [Fact]
    public void ExtractContentHandlesStringAndPartsArray()
    {
        Assert.Equal("{}", OpenAiCompatibleReceiptRecognizer.ExtractContent("""{"choices":[{"message":{"content":"{}"}}]}"""));
        Assert.Equal("ab", OpenAiCompatibleReceiptRecognizer.ExtractContent(
            """{"choices":[{"message":{"content":[{"type":"text","text":"a"},{"type":"text","text":"b"}]}}]}"""));
        Assert.Null(OpenAiCompatibleReceiptRecognizer.ExtractContent("""{"error":"x"}"""));
        Assert.Null(OpenAiCompatibleReceiptRecognizer.ExtractContent("not json"));
    }

    [Fact]
    public void ModelUriIsBuiltFromFolder()
    {
        Assert.Equal("gpt://b1g/qwen", new ReceiptRecognitionOptions { Project = "b1g", Model = "qwen" }.ResolveModel());
        Assert.Equal("gpt://other/m", new ReceiptRecognitionOptions { Project = "b1g", Model = "gpt://other/m" }.ResolveModel());
        Assert.Equal("gpt-4o", new ReceiptRecognitionOptions { Model = "gpt-4o" }.ResolveModel());
        Assert.False(new ReceiptRecognitionOptions { ApiKey = "" }.IsEnabled);
        Assert.True(new ReceiptRecognitionOptions { ApiKey = "k" }.IsEnabled);
    }

    [Fact]
    public void MonthParsing()
    {
        Assert.True(FinanceMonth.TryParse("2026-11", out var month));
        Assert.Equal(new DateOnly(2026, 11, 1), month.From);
        Assert.Equal(new DateOnly(2026, 12, 1), month.ToExclusive);
        Assert.Equal("2026-10", month.Previous.ToString());
        Assert.False(FinanceMonth.TryParse("2026-13", out _));
        Assert.False(FinanceMonth.TryParse("11.2026", out _));
        Assert.False(FinanceMonth.TryParse(null, out _));
    }
}
