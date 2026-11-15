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
        Assert.Equal(7m, ReceiptRecognitionParser.Parse("""Скобка { без пары, ответ: {"total": 7}""")!.Total);
    }

    [Fact]
    public void PromptEchoBeforeTruncatedAnswerIsIgnored()
    {
        var response = "Схема: " + """{"isReceipt": true, "store": """ + "\"" + ReceiptRecognitionPrompt.StorePlaceholder + "\""
                       + """, "total": 1234.56, "items": [{"name": "x", "amount": 99.90}]}""" + "\nОтвет: "
                       + """{"isReceipt":true,"store":"Магнит","total":5320.40,"items":[{"name":"Моло""";

        var parsed = ReceiptRecognitionParser.Parse(response);

        Assert.Equal("Магнит", parsed!.Store);
        Assert.Equal(5320.40m, parsed.Total);
        Assert.True(parsed.Repaired);
    }

    [Fact]
    public void ItemExampleBeforeTruncatedAnswerDoesNotWin()
    {
        var parsed = ReceiptRecognitionParser.Parse(
            """Например {"name":"Хлеб","amount":45}. Ответ: {"isReceipt":true,"total":5320.40,"items":[{"name":"Сыр","amount":300},{"na""");

        Assert.Equal(5320.40m, parsed!.Total);
        Assert.Equal("Сыр", Assert.Single(parsed.Items).Name);
    }

    [Fact]
    public void ReasoningWithoutOpeningTagIsDropped()
    {
        var parsed = ReceiptRecognitionParser.Parse(
            """Начну черновик: {"isReceipt": true, "store": "Пятерочка", "total": 100, ... </think>{"isReceipt": true, "total": 1520.00, "items": [{"name": "A", "amount": 1000}, {"name": "B", "amount": 520}]}""");

        Assert.Equal(1520m, parsed!.Total);
        Assert.Equal(2, parsed.Items.Count);
    }

    [Fact]
    public void TrailingHypotheticalObjectDoesNotOverrideAnswer()
    {
        var parsed = ReceiptRecognitionParser.Parse(
            """{"isReceipt": true, "store": "Кафе", "total": 300, "items": []} Если бы на фото не было чека, ответ был бы {"isReceipt": false}""");

        Assert.True(parsed!.IsReceipt);
        Assert.Equal(300m, parsed.Total);
    }

    private const string FullAnswer = """{"isReceipt": true, "store": "Магнит", "total": 1520, "items": [{"name": "A", "amount": 1000, "category": "HOME"}, {"name": "B", "amount": 520, "category": "PETS"}]}""";

    [Theory]
    [InlineData("""{"isReceipt": true, "store": "Магнит",""" + "\n")]
    [InlineData("""{"total": 1500, "items": [""")]
    [InlineData("""{"isReceipt": true, "store": "название магазина или организации, null если не видно",""" + "\n")]
    public void AbandonedObjectBeforeFullAnswerDoesNotSwallowIt(string prefix)
    {
        var parsed = ReceiptRecognitionParser.Parse(prefix + FullAnswer);

        Assert.Equal(1520m, parsed!.Total);
        Assert.Equal(2, parsed.Items.Count);
        Assert.False(parsed.Repaired);
    }

    [Fact]
    public void TruncatedRepeatDoesNotBeatFullAnswer()
    {
        var parsed = ReceiptRecognitionParser.Parse(FullAnswer + "\n" + FullAnswer[..90]);

        Assert.Equal(1520m, parsed!.Total);
        Assert.Equal(2, parsed.Items.Count);
    }

    [Fact]
    public void CopiedFieldDescriptionsDoNotSpoilAnswer()
    {
        var parsed = ReceiptRecognitionParser.Parse(
            """{"isReceipt": true, "store": "название магазина или организации, null если не видно", "currency": "код валюты ISO 4217, например RUB", "total": 300}""");

        Assert.Null(parsed!.Store);
        Assert.Equal("RUB", parsed.Currency);
        Assert.Equal(300m, parsed.Total);
    }

    [Fact]
    public void ExampleTotalIsComparedAsValueNotSubstring()
    {
        var parsed = ReceiptRecognitionParser.Parse(
            """{"isReceipt": true, "store": "название магазина или организации, null если не видно", "total": 11300, "items": [{"name": "A", "amount": 11234.56}]}""");

        Assert.Equal(11300m, parsed!.Total);
        Assert.Null(parsed.Store);
    }

    [Fact]
    public void ManyUnclosedBracesAreParsedQuickly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ReceiptRecognitionParser.Parse(string.Concat(Enumerable.Repeat("{\"a\":", 100_000)));
        ReceiptRecognitionParser.Parse(new string('{', 2_000_000));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    [Theory]
    [InlineData(null, "RUB")]
    [InlineData("руб.", "RUB")]
    [InlineData("$", "USD")]
    [InlineData("€", "EUR")]
    [InlineData("EURO", "EUR")]
    [InlineData("JPY", "JPY")]
    [InlineData("¥", "XXX")]
    [InlineData("US dollars", "XXX")]
    public void CurrencyIsNeverSilentlyRubles(string? value, string expected)
    {
        Assert.Equal(expected, ReceiptRecognitionParser.NormalizeCurrency(value));
    }

    [Fact]
    public void NonPositiveItemsSumGoesToReceiptCategory()
    {
        var result = FinanceAllocation.Allocate(10m, FinanceCategory.CAFE,
            [(FinanceCategory.GROCERIES, 100m), (FinanceCategory.OTHER, -150m)]);

        Assert.Equal(10m, result[FinanceCategory.CAFE]);
        Assert.Single(result);
    }

    [Theory]
    [InlineData("1 234,50", 1234.50)]
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("-50,00 ₽", -50.0)]
    [InlineData("\u221210,00", -10.0)]
    [InlineData("1 250,00 р.", 1250.0)]
    [InlineData("89,90 руб.", 89.90)]
    [InlineData("1\u202F234,5", 1234.5)]
    [InlineData("1,234", 1234.0)]
    [InlineData("1e3", null)]
    [InlineData("abc", null)]
    public void AmountParsing(string text, double? expected)
    {
        Assert.Equal(expected is null ? null : (decimal)expected.Value, ReceiptRecognitionParser.ParseAmount(text));
    }

    [Fact]
    public void QuantityKeepsThreeDecimals()
    {
        Assert.Equal(1.234m, ReceiptRecognitionParser.ParseAmount("1,234", maxFractionDigits: 3));
        Assert.Equal(1.5m, ReceiptRecognitionParser.ParseAmount("1.5 кг.", maxFractionDigits: 3));
    }

    [Fact]
    public void ParserKeepsSurrogatePairsIntactWhenTruncating()
    {
        var name = new string('а', 199) + "😀хвост";
        var parsed = ReceiptRecognitionParser.Parse($$"""{"total": 10, "items": [{"name": "{{name}}", "amount": 10}]}""");

        var stored = parsed!.Items.Single().Name;
        Assert.Equal(199, stored.Length);
        Assert.DoesNotContain(stored, char.IsSurrogate);
        Assert.Null(ReceiptRecognitionParser.Parse("""{"store": "x\ud83d", "total": 1}"""));
        Assert.Equal("ab", TextSafety.Clean("a\ud83db\0"));
    }

    [Fact]
    public void TruncatedAnswerKeepsTotalAndCompleteItems()
    {
        const string response = """{"isReceipt":true,"store":"Магнит","total":5320.40,"items":[{"name":"Молоко","amount":89.9,"category":"GROCERIES"},{"name":"Хлеб","amount":45,"category":"GROC""";

        var parsed = ReceiptRecognitionParser.Parse(response);

        Assert.Equal(5320.40m, parsed!.Total);
        Assert.Equal(["Молоко", "Хлеб"], parsed.Items.Select(x => x.Name));
        // Сумма «Хлеба» дописана целиком (обрыв был дальше); недописанная категория → OTHER.
        Assert.Equal(45m, parsed.Items[1].Amount);
        Assert.Equal(FinanceCategory.OTHER, parsed.Items[1].Category);

        var cutInNumber = ReceiptRecognitionParser.Parse("""{"total": 100, "items": [{"name": "Сыр", "amount": 45""");
        Assert.Equal(100m, cutInNumber!.Total);
        Assert.Empty(cutInNumber.Items);
    }

    [Fact]
    public void ReasoningBlockAndPromptEchoAreIgnored()
    {
        const string response = """
            <think>Схема требует {"isReceipt": false}, если это не чек. Здесь чек.</think>
            {"isReceipt": true, "total": 120}
            """;

        var parsed = ReceiptRecognitionParser.Parse(response);

        Assert.True(parsed!.IsReceipt);
        Assert.Equal(120m, parsed.Total);
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
        // Yandex без каталога отклонит каждый запрос — такое распознавание считаем выключенным.
        Assert.False(new ReceiptRecognitionOptions { ApiKey = "k" }.IsEnabled);
        Assert.True(new ReceiptRecognitionOptions { ApiKey = "k", Project = "b1g" }.IsEnabled);
        Assert.True(new ReceiptRecognitionOptions { ApiKey = "k", BaseUrl = "https://llm.example/v1", Model = "m" }.IsEnabled);
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
