namespace Application.Modules.Finance;

/// <summary>
/// Делит итог чека между категориями позиций пропорционально их суммам.
/// Итог чека главный: скидки, округления и нераспознанные строки «размазываются» по категориям,
/// а сумма долей всегда в точности равна итогу (метод наибольшего остатка, шаг — копейка).
/// </summary>
public static class FinanceAllocation
{
    public static Dictionary<FinanceCategory, decimal> Allocate(
        decimal total,
        FinanceCategory receiptCategory,
        IEnumerable<(FinanceCategory Category, decimal Amount)> items)
    {
        var result = new Dictionary<FinanceCategory, decimal>();
        total = Math.Round(total, 2, MidpointRounding.AwayFromZero);
        if (total <= 0)
            return result;

        var list = items.ToList();
        // Сумма позиций не положительна (скидки больше покупок — ответ модели явно неверен): весь итог — категории чека.
        if (list.Sum(x => x.Amount) <= 0)
        {
            result[receiptCategory] = total;
            return result;
        }

        // Веса — только положительные суммы категорий: отдельная строка скидки уменьшает итог,
        // но не должна давать категории отрицательную долю.
        var weights = list
            .GroupBy(x => x.Category)
            .Select(g => (Category: g.Key, Weight: g.Sum(x => x.Amount)))
            .Where(x => x.Weight > 0)
            .OrderBy(x => x.Category)
            .ToList();

        var weightSum = weights.Sum(x => x.Weight);
        if (weightSum <= 0)
        {
            result[receiptCategory] = total;
            return result;
        }

        var totalCents = (long)(total * 100);
        var shares = weights
            .Select(x =>
            {
                var exact = totalCents * x.Weight / weightSum;
                var floor = (long)Math.Floor(exact);
                return (x.Category, Cents: floor, Remainder: exact - floor);
            })
            .ToList();

        var left = totalCents - shares.Sum(x => x.Cents);
        // Остаток копеек — категориям с наибольшей дробной частью (при равенстве — по порядку кода, детерминированно).
        var order = shares
            .Select((x, index) => (index, x.Remainder))
            .OrderByDescending(x => x.Remainder)
            .ThenBy(x => x.index)
            .Select(x => x.index)
            .ToList();
        for (var i = 0; left > 0; i = (i + 1) % order.Count, left--)
        {
            var index = order[i];
            shares[index] = shares[index] with { Cents = shares[index].Cents + 1 };
        }

        foreach (var share in shares.Where(x => x.Cents > 0))
            result[share.Category] = share.Cents / 100m;

        return result;
    }

    public static Dictionary<FinanceCategory, decimal> Allocate(FinanceReceipt receipt)
    {
        return Allocate(receipt.Total, receipt.Category, receipt.Items.Select(x => (x.Category, x.Amount)));
    }

    /// <summary>Категория с наибольшей суммой позиций — категория чека целиком.</summary>
    public static FinanceCategory? DominantCategory(IEnumerable<(FinanceCategory Category, decimal Amount)> items)
    {
        var best = items
            .GroupBy(x => x.Category)
            .Select(g => (Category: g.Key, Sum: g.Sum(x => x.Amount)))
            .Where(x => x.Sum > 0)
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.Category)
            .FirstOrDefault();
        return best.Sum > 0 ? best.Category : null;
    }
}
