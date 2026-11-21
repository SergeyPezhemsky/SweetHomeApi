using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Application.Modules.Coins;

namespace SweetHomeApi.Controllers.Coins;

public sealed class UpsertCoinDto
{
    [Required, StringLength(96)]
    public string Name { get; init; } = string.Empty;

    [StringLength(60)]
    public string? Country { get; init; }

    [Range(-5000, 9999)]
    public int? Year { get; init; }

    [StringLength(50)]
    public string? Value { get; init; }

    [RegularExpression("G|VG|F|VF|XF|AU|UNC|Proof")]
    public string? Grade { get; init; }

    [Range(1, 9999)]
    public int Quantity { get; init; } = 1;

    [StringLength(120)]
    public string? Location { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public CoinPhotoChange? Obverse { get; init; }
    public CoinPhotoChange? Reverse { get; init; }
}

public sealed record CoinPhotoChange(bool Remove, byte[]? Data);

public sealed record CoinDto(
    Guid Id, string Name, string? Country, int? Year, string? Value, string? Grade, int Quantity,
    string? Location, string? Notes, bool HasObverse, bool HasReverse, DateTime CreatedAt, DateTime UpdatedAt)
{
    internal static readonly Expression<Func<Coin, CoinDto>> Projection = coin => new CoinDto(
        coin.Id, coin.Name, coin.Country, coin.Year, coin.Value, coin.Grade, coin.Quantity,
        coin.Location, coin.Notes, coin.Obverse != null, coin.Reverse != null, coin.CreatedAt, coin.UpdatedAt);
}
