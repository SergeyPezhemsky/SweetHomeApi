using System.Text;

namespace Application.Modules.Finance;

/// <summary>
/// Ключ позиции для правил категорий: первые два значимых слова названия.
/// «Корм Purina Pro Plan 1,5 кг» и «КОРМ PURINA для щенков» → «корм purina».
/// </summary>
public static class FinanceItemKey
{
    private const int MaxWords = 2;
    private const int MinWordLength = 3;

    /// <summary>Длина столбца Key в FinanceCategoryRules.</summary>
    public const int MaxLength = 100;

    private static readonly HashSet<string> StopWords =
    [
        "для", "без", "при", "или", "под", "над", "про", "шт", "упак", "уп", "вес", "весовой", "весовая", "весовые",
        "the", "and", "for", "with", "pcs"
    ];

    public static string? Of(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var builder = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
            builder.Append(c == 'ё' ? 'е' : char.IsLetter(c) ? c : ' ');

        var words = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= MinWordLength && !StopWords.Contains(x))
            .Take(MaxWords)
            .ToList();

        if (words.Count == 0)
            return null;

        var key = string.Join(' ', words);
        return key.Length > MaxLength ? key[..MaxLength] : key;
    }
}
