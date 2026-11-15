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

        try
        {
            using (document)
                return ReadReceipt(document.RootElement);
        }
        catch (InvalidOperationException)
        {
            // Например, одиночный суррогат в строке: GetString() не может его декодировать.
            return null;
        }
    }

    private static RecognizedReceipt? ReadReceipt(JsonElement root)
    {
        {
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

                    var quantity = GetDecimal(item, "quantity", maxFractionDigits: 3);
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

    /// <summary>
    /// JSON-объект ответа. Модель может обернуть его в ```json, пояснения или блок рассуждений &lt;think&gt;:
    /// рассуждения выкидываем, из нескольких объектов верхнего уровня берём последний (ответ идёт после примеров).
    /// Если ответ оборван (закончились токены), достраиваем объект до последнего целого значения —
    /// так итог и уже выписанные позиции не теряются.
    /// </summary>
    public static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = ThinkBlock.Replace(text, string.Empty);
        // Незакрытый блок рассуждений (оборванный ответ) — всё после него рассуждение.
        var openThink = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (openThink >= 0)
            text = text[..openThink];

        string? last = null;
        var lastStart = -1;
        var firstUnclosed = -1;
        var position = 0;
        while (position < text.Length)
        {
            var start = text.IndexOf('{', position);
            if (start < 0)
                break;

            var end = FindObjectEnd(text, start);
            if (end < 0)
            {
                // Не закрыт до конца текста: либо оборванный ответ, либо случайная «{» в пояснении перед ответом.
                if (firstUnclosed < 0)
                    firstUnclosed = start;
                position = start + 1;
                continue;
            }

            var candidate = text[start..(end + 1)];
            if (IsValidJson(candidate))
            {
                last = candidate;
                lastStart = start;
                position = end + 1;
            }
            else
            {
                position = start + 1;
            }
        }

        // Незакрытый объект раньше найденного: найденный может быть позицией внутри оборванного ответа.
        // Если оборванный объект удаётся достроить — берём его (в нём итог), иначе это была случайная скобка.
        if (firstUnclosed >= 0 && (last is null || firstUnclosed < lastStart))
            return RepairTruncated(text, firstUnclosed) ?? last;

        return last;
    }

    private static readonly System.Text.RegularExpressions.Regex ThinkBlock = new(
        "<think>.*?</think>",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Индекс закрывающей скобки объекта, начинающегося в start; -1 — если текст кончился раньше.</summary>
    private static int FindObjectEnd(string text, int start)
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
            else if (c is '{' or '[')
                depth++;
            else if (c is '}' or ']' && --depth == 0)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Достраивает оборванный JSON: идём назад по закрывающим скобкам и пробуем «обрезать здесь
    /// и закрыть всё открытое». Первая удачная попытка сохраняет максимум целых значений.
    /// </summary>
    private static string? RepairTruncated(string text, int start)
    {
        var attempts = 0;
        for (var cut = text.Length - 1; cut > start && attempts < 300; cut--)
        {
            // Режем только там, где предыдущее значение точно закончилось: после «}», «]», «,» или в начале массива.
            // Обрыв посреди числа («45» из «45.90») дал бы неверную сумму.
            if (text[cut] is not ('}' or ']' or ',' or '['))
                continue;

            attempts++;
            var prefix = text[start..(cut + 1)];
            var stack = new Stack<char>();
            var inString = false;
            var escaped = false;
            foreach (var c in prefix)
            {
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
                else if (c is '{' or '[')
                    stack.Push(c);
                else if (c is '}' or ']' && stack.Count > 0)
                    stack.Pop();
            }

            if (inString || stack.Count == 0)
                continue;

            var builder = new StringBuilder(prefix.TrimEnd().TrimEnd(','));
            while (stack.Count > 0)
                builder.Append(stack.Pop() == '{' ? '}' : ']');

            var candidate = builder.ToString();
            if (IsValidJson(candidate))
                return candidate;
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

    /// <summary>
    /// Сумма из строки: «1 234,50», «1.234,50», «1,234.50», «89,90 р.», «−50,00 ₽».
    /// Разделитель считается десятичным, только если за ним 1..maxFractionDigits цифр и он последний;
    /// буквы между цифрами («1e3») — не число.
    /// </summary>
    public static decimal? ParseAmount(string? value, int maxFractionDigits = 2)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var first = -1;
        var last = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
                continue;
            if (first < 0)
                first = i;
            last = i;
        }

        if (first < 0)
            return null;

        // Минус — любой вид тире перед первой цифрой (модели пишут и U+2212).
        var negative = text[..first].Any(c => c is '-' or '\u2212' or '\u2012' or '\u2013' or '\u2014' or '\u2010' or '\u2011');

        var digits = new StringBuilder();
        var lastSeparator = -1;
        foreach (var c in text[first..(last + 1)])
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
            else if (c is '.' or ',')
            {
                lastSeparator = digits.Length;
                digits.Append('.');
            }
            else if (!char.IsWhiteSpace(c) && c is not ('\'' or '\u2019'))
            {
                return null;
            }
        }

        var raw = digits.ToString();
        string normalized;
        var fraction = lastSeparator >= 0 ? raw.Length - lastSeparator - 1 : 0;
        if (lastSeparator >= 0 && fraction >= 1 && fraction <= maxFractionDigits)
            normalized = raw[..lastSeparator].Replace(".", string.Empty) + "." + raw[(lastSeparator + 1)..];
        else
            normalized = raw.Replace(".", string.Empty);

        if (normalized.Length == 0 || normalized.Length > 20
            || !decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return null;

        return negative ? -amount : amount;
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

    private static decimal? GetDecimal(JsonElement element, string name, int maxFractionDigits = 2)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String => ParseAmount(value.GetString(), maxFractionDigits),
            _ => null
        };
    }

    private static string? CleanText(string? value, int maxLength)
    {
        if (value is null)
            return null;

        var text = TextSafety.Clean(value).Trim();
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        return TextSafety.Truncate(text, maxLength).TrimEnd();
    }
}

/// <summary>Текст, который PostgreSQL (UTF-8, без NUL) примет без ошибок.</summary>
public static class TextSafety
{
    /// <summary>Убирает управляющие символы и одиночные суррогаты (их нельзя закодировать в UTF-8).</summary>
    public static string Clean(string value, bool keepLineBreaks = false)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    builder.Append(c).Append(value[i + 1]);
                    i++;
                }

                continue;
            }

            if (char.IsLowSurrogate(c))
                continue;
            if (char.IsControl(c) && !(keepLineBreaks && c is '\n' or '\r' or '\t'))
                continue;

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Обрезает до maxLength UTF-16 единиц, не разрывая суррогатную пару.</summary>
    public static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var length = maxLength;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        return value[..length];
    }
}
