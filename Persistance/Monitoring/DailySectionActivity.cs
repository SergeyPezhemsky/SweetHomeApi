namespace Persistance.Monitoring;

public sealed class DailySectionActivity
{
    public DateOnly Day { get; set; }
    public string UserId { get; set; } = null!;
    public string Section { get; set; } = null!;
    public long Visits { get; set; }
    public DateTime LastSeenAt { get; set; }
}
