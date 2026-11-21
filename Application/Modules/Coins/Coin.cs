namespace Application.Modules.Coins;

public sealed class Coin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public int? Year { get; set; }
    public string? Value { get; set; }
    public string? Grade { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public byte[]? Obverse { get; set; }
    public byte[]? Reverse { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
