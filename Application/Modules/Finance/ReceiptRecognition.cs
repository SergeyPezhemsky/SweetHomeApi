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

    /// <summary>Ответ был оборван и достроен: без явного итога такому чеку верить нельзя.</summary>
    public bool Repaired { get; set; }
}

public record RecognizedItem(string Name, decimal? Quantity, decimal? Price, decimal Amount, FinanceCategory Category);

/// <summary>Промпт для vision-модели: какие поля и категории вернуть.</summary>
public static class ReceiptRecognitionPrompt
{
    public static string System { get; } = BuildSystem();

    public const string User = "Распознай чек на фото и верни JSON строго по схеме.";

    /// <summary>Значения-заглушки из примера схемы: объект с ними — эхо промпта, а не ответ.</summary>
    public const string StorePlaceholder = "название магазина или организации, null если не видно";
    public const string ItemPlaceholder = "название позиции как на чеке";
    public const string ExampleTotal = "1234.56";

    private static string BuildSystem()
    {
        var categories = string.Join('\n', FinanceDictionaries.Categories.Select(x => $"- {x.Code}: {x.Name} — {x.Hint}"));
        return $$"""
            Ты распознаёшь кассовые чеки, квитанции и скриншоты оплат на фото.
            Ответь ТОЛЬКО одним JSON-объектом без пояснений и без markdown.

            Схема:
            {
              "isReceipt": true,
              "store": "{{StorePlaceholder}}",
              "date": "дата покупки YYYY-MM-DD, null если не видно",
              "currency": "код валюты ISO 4217, например RUB",
              "total": {{ExampleTotal}},
              "category": "код категории для чека целиком",
              "items": [
                { "name": "{{ItemPlaceholder}}", "quantity": 1, "price": 99.90, "amount": 99.90, "category": "код категории" }
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
        var extracted = Extract(response);
        if (extracted is null)
            return null;

        var (json, repaired) = extracted.Value;

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
            {
                var receipt = ReadReceipt(document.RootElement);
                if (receipt is not null)
                    receipt.Repaired = repaired;
                return receipt;
            }
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
                Store = CleanStore(GetString(root, "store")),
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

    /// <summary>Сколько текста ответа разбираем и сколько стартов «{» проверяем: защита от квадратичного разбора.</summary>
    public const int MaxScanLength = 256 * 1024;
    private const int MaxObjectStarts = 200;
    private const int MaxRepairAttempts = 3;

    /// <summary>Ключи, по которым объект узнаётся как чек. «category» есть и у позиции, поэтому считается только вместе с ними.</summary>
    private static readonly string[] ReceiptKeys = ["isReceipt", "total", "items", "store", "date", "currency"];

    public static string? ExtractJsonObject(string? text) => Extract(text)?.Json;

    /// <summary>
    /// JSON-объект ответа. Рассуждения модели (&lt;think&gt;…&lt;/think&gt;, в том числе без открывающего тега) отбрасываем.
    /// Кандидаты — объекты верхнего уровня (вложенные в другой кандидат не считаются) и оборванный ответ, достроенный
    /// до последнего целого значения. Побеждает объект, больше всего похожий на чек (ключи total, items, …);
    /// эхо примера из промпта отбрасывается; при равенстве — более поздний.
    /// </summary>
    private static (string Json, bool Repaired)? Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Qwen3 в режиме рассуждений может отдать «…</think>» без открывающего тега: ответ — после последнего закрывающего.
        var closeThink = text.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (closeThink >= 0)
            text = text[(closeThink + "</think>".Length)..];
        text = ThinkBlock.Replace(text, string.Empty);
        // Незакрытый блок рассуждений (оборванный ответ) — всё после него рассуждение.
        var openThink = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (openThink >= 0)
            text = text[..openThink];
        if (text.Length > MaxScanLength)
            text = text[..MaxScanLength];

        var candidates = new List<(int Start, int End, string Json, bool Repaired)>();
        var unclosed = new List<int>();
        var position = 0;
        var starts = 0;
        while (position < text.Length && starts < MaxObjectStarts)
        {
            var start = text.IndexOf('{', position);
            if (start < 0)
                break;

            starts++;
            var end = FindObjectEnd(text, start);
            if (end < 0)
            {
                // Не закрыт до конца текста: оборванный ответ или случайная «{» в пояснении.
                unclosed.Add(start);
                position = start + 1;
                continue;
            }

            var candidate = text[start..(end + 1)];
            if (IsValidJson(candidate))
            {
                candidates.Add((start, end + 1, candidate, false));
                position = end + 1;
            }
            else
            {
                position = start + 1;
            }
        }

        // Внешний незакрытый объект — корень оборванного ответа; если не достраивается — пробуем следующий.
        foreach (var start in unclosed.Take(MaxRepairAttempts))
        {
            if (RepairTruncated(text, start) is { } repaired)
            {
                // Диапазон — только использованный префикс: полные объекты после точки обрезки остаются кандидатами.
                candidates.Add((start, repaired.End, repaired.Json, true));
                break;
            }
        }

        if (candidates.Count == 0)
            return null;

        // Полные объекты внутри достроенного (позиции оборванного ответа) не отбрасываем заранее: у позиции
        // нет ключей чека, и ранжирование само отдаст первенство чеку. При равенстве полный ответ надёжнее
        // достроенного (обрезанный повтор не должен терять позиции), затем — более поздний.
        var best = candidates
            .Select(c => (Candidate: c, Score: ReceiptScore(c.Json)))
            .Where(x => x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.Repaired)
            .ThenByDescending(x => x.Candidate.Start)
            .Select(x => x.Candidate)
            .FirstOrDefault();

        return best.Json is null ? null : (best.Json, best.Repaired);
    }

    /// <summary>Сколько ключей чека есть у объекта; -1 — эхо примера из промпта.</summary>
    private static int ReceiptScore(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return -1;

            // Эхо примера — по заглушке позиции; заглушку магазина модель может скопировать в настоящий ответ,
            // её превращаем в null при чтении.
            if (json.Contains(ReceiptRecognitionPrompt.ItemPlaceholder, StringComparison.Ordinal)
                || (json.Contains(ReceiptRecognitionPrompt.StorePlaceholder, StringComparison.Ordinal)
                    && json.Contains(ReceiptRecognitionPrompt.ExampleTotal, StringComparison.Ordinal)))
                return -1;

            var score = ReceiptKeys.Count(key => TryGetProperty(root, key, out _));
            return score > 0 && TryGetProperty(root, "category", out _) ? score + 1 : score;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return -1;
        }
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
    private static (string Json, int End)? RepairTruncated(string text, int start)
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
                return (candidate, cut + 1);
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

    /// <summary>Код валюты, который не удалось определить: такой чек не попадёт в рублёвые итоги.</summary>
    public const string UnknownCurrency = "XXX";

    /// <summary>
    /// Рубли — для пустого значения и рублёвых обозначений; известные символы — в коды ISO;
    /// всё непонятное — <see cref="UnknownCurrency"/>, а не молча рубли (50 € не должны стать 50 ₽).
    /// </summary>
    public static string NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FinanceDefaults.Currency;

        var code = value.Trim().TrimEnd('.').ToUpperInvariant();
        // Модель скопировала описание поля из схемы («код валюты ISO 4217, например RUB») — валюту не определила.
        if (code.Contains("ISO 4217", StringComparison.Ordinal))
            return FinanceDefaults.Currency;

        switch (code)
        {
            // «P» латиницей — частая замена знака рубля на кассовых лентах.
            case "₽" or "Р" or "P" or "РУБ" or "РУБЛЬ" or "РУБЛЕЙ" or "RUR" or "RUB" or "RUBLE" or "RUBLES":
                return FinanceDefaults.Currency;
            case "$" or "US$" or "USD" or "ДОЛЛ" or "ДОЛЛАР":
                return "USD";
            case "€" or "EUR" or "EURO" or "ЕВРО":
                return "EUR";
            case "£" or "GBP":
                return "GBP";
            case "₸" or "ТГ" or "ТЕНГЕ":
                return "KZT";
            case "₺":
                return "TRY";
            case "₾":
                return "GEL";
            case "֏":
                return "AMD";
            case "BR" or "БЕЛ. РУБ" or "BYN":
                return "BYN";
            case "元" or "人民币":
                return "CNY";
            case "円":
                return "JPY";
        }

        // «¥» — и юань, и иена; угадывать не будем.
        return code.Length == 3 && code.All(c => c is >= 'A' and <= 'Z') ? code : UnknownCurrency;
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

    /// <summary>Магазин; скопированное из схемы описание поля («название магазина…») — значит, магазин не виден.</summary>
    private static string? CleanStore(string? value)
    {
        var store = CleanText(value, FinanceDefaults.MaxStoreLength);
        return store is not null && store.StartsWith("название магазина", StringComparison.OrdinalIgnoreCase)
            ? null
            : store;
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
