namespace Persistance.Monitoring;

public sealed class ApiFailure
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public int StatusCode { get; set; }
    public string Method { get; set; } = null!;
    public string Route { get; set; } = null!;
    public string? ExceptionType { get; set; }
    public string TraceId { get; set; } = null!;
}
