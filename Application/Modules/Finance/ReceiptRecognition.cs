using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Modules.Finance.Seeds;

namespace Application.Modules.Finance;

/// <summary>Ответ модели, разобранный в типизированный вид (ещё без правил пользователя и проверки даты).</summary>
public class RecognizedReceipt
{
    public bool IsReceipt { get; set; }
    public string? Store { get; set; }
    public DateOnly? Date { get; set; }
    public string Currency { get; set; } = FinanceDefaults.Currency;
    public decimal? Total { get; set; }
    public FinanceCategory? Category { get; set; }
    public List<RecognizedItem> Items { get; set; } = [];
}

public record RecognizedItem(string Name, decimal? Quantity, decimal? Price, decimal Amount, FinanceCategory Category);

/// <summary>Промпт для vision-модели: какие поля и категории вернуть.</summary>
public static class ReceiptRecognitionPrompt
{
    public static string System { get; } = BuildSystem();

    public const string User = "Распознай чек на фото и верни JSON строго по схеме.";

    private static string BuildSystem()
    {
        var categories = string.Join('\n', FinanceDictionaries.Categories.Select(x => $"- {x.Code}: {x.Name} — {x.Hint}"));
        return $$"""
            Ты распознаёшь кассовые чеки, квитанции и скриншоты оплат на фото.
            Ответь ТОЛЬКО одним JSON-объектом без пояснений и без markdown.

            Схема:
            {
              "isReceipt": true,
              "store": "название магазина или организации, null если не видно",
              "date": "дата покупки YYYY-MM-DD, null если не видно",
              "currency": "код валюты ISO 4217, например RUB",
              "total": 1234.56,
              "category": "код категории для чека целиком",
              "items": [
                { "name": "название позиции как на чеке", "quantity": 1, "price": 99.90, "amount": 99.90, "category": "код категории" }
              ]
            }

            Правила:
            - total — итоговая сумма к оплате (строка «ИТОГ», «К оплате», «Итого»). Числа — без пробелов, с точкой.
            - amount — сумма позиции после скидки на эту позицию. Если скидка на весь чек напечатана отдельной строкой,
              добавь её как позицию с отрицательной суммой.
            - Не добавляй в items строки итогов, НДС, сдачи, способа оплаты, бонусов.
            - Если позиции не видны (например, скриншот перевода), верни пустой items и укажи total и category.
            - Если на фото нет чека или оплаты, верни {"isReceipt": false}.
            - category — один из кодов:
            {{categories}}
            """;
    }
}

/// <summary>
/// Разбор ответа модели. Терпим к «мусору» вокруг JSON, числам-строкам с пробелами и запятой,
/// неизвестным категориям. Возвращает null, если JSON-объекта в ответе нет.
/// </summary>
public static class ReceiptRecognitionParser
{
    public static RecognizedReceipt? Parse(string? response)
    {
        var json = ExtractJsonObject(response);
        if (json is null)
            return null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var result = new RecognizedReceipt
            {
                IsReceipt = GetBool(root, "isReceipt") ?? true,
                Store = CleanText(GetString(root, "store"), FinanceDefaults.MaxStoreLength),
                Date = ParseDate(GetString(root, "date")),
                Currency = NormalizeCurrency(GetString(root, "currency")),
                Total = PositiveAmount(GetDecimal(root, "total")),
                Category = ParseCategory(GetString(root, "category"))
            };

            if (TryGetProperty(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (result.Items.Count >= FinanceDefaults.MaxItems)
                        break;
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var name = CleanText(GetString(item, "name"), FinanceDefaults.MaxItemNameLength);
                    var amount = RoundMoney(GetDecimal(item, "amount") ?? GetDecimal(item, "sum"));
                    if (name is null || amount is null or 0 || Math.Abs(amount.Value) > FinanceDefaults.MaxAmount)
                        continue;

                    var quantity = GetDecimal(item, "quantity");
                    var price = RoundMoney(GetDecimal(item, "price"));
                    result.Items.Add(new RecognizedItem(
                        name,
                        quantity is > 0 and < 1_000_000 ? Math.Round(quantity.Value, 3, MidpointRounding.AwayFromZero) : null,
                        price is >= 0 and <= FinanceDefaults.MaxAmount ? price : null,
                        amount.Value,
                        ParseCategory(GetString(item, "category")) ?? FinanceCategory.OTHER));
                }
            }

            return result;
        }
    }

    /// <summary>Первый сбалансированный JSON-объект в тексте (модель может обернуть его в ```json или пояснения).</summary>
    public static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        for (var start = text.IndexOf('{'); start >= 0; start = text.IndexOf('{', start + 1))
        {
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
                else if (c == '}' && --depth == 0)
                {
                    var candidate = text[start..(i + 1)];
                    if (IsValidJson(candidate))
                        return candidate;
                    break;
                }
            }
        }

        return null;
    }

    public static FinanceCategory? ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var code = value.Trim().ToUpperInvariant();
        if (code.Any(c => !char.IsLetter(c) && c != '_'))
            return null;

        return Enum.TryParse<FinanceCategory>(code, false, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    public static string NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FinanceDefaults.Currency;

        var code = value.Trim().ToUpperInvariant();
        if (code is "₽" or "Р" or "РУБ" or "РУБ." or "RUR" or "Р." or "RUBLE" or "RUBLES")
            return FinanceDefaults.Currency;

        return code.Length == 3 && code.All(c => c is >= 'A' and <= 'Z') ? code : FinanceDefaults.Currency;
    }

    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        // Время после даты не нужно: «2026-11-14T18:32», «14.11.2026 18:32».
        var cut = text.IndexOfAny(['T', ' ']);
        if (cut > 0)
            text = text[..cut];

        string[] formats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "yyyy.MM.dd", "dd/MM/yyyy", "dd-MM-yyyy"];
        return DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public static decimal? ParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var builder = new StringBuilder();
        foreach (var c in value.Trim())
        {
            if (char.IsDigit(c) || c == '-')
                builder.Append(c);
            else if (c is ',' or '.')
                builder.Append('.');
            // Пробелы (в т.ч. неразрывные) — разделители тысяч; символы валют отбрасываем.
        }

        var text = builder.ToString();
        // «1.234.56» → оставляем только последнюю точку как десятичную.
        var lastDot = text.LastIndexOf('.');
        if (lastDot >= 0)
            text = text[..lastDot].Replace(".", string.Empty) + text[lastDot..];

        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    private static bool IsValidJson(string candidate)
    {
        try
        {
            using var _ = JsonDocument.Parse(candidate, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static decimal? PositiveAmount(decimal? value)
    {
        var rounded = RoundMoney(value);
        return rounded is > 0 && rounded <= FinanceDefaults.MaxAmount ? rounded : null;
    }

    private static decimal? RoundMoney(decimal? value)
    {
        return value is null ? null : Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        // Имена полей без учёта регистра: модели иногда пишут "Total" или "ITEMS".
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static bool? GetBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String => ParseAmount(value.GetString()),
            _ => null
        };
    }

    private static string? CleanText(string? value, int maxLength)
    {
        if (value is null)
            return null;

        var text = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        return text.Length > maxLength ? text[..maxLength].TrimEnd() : text;
    }
}
