namespace SweetHomeApi.Infrastructure.Monitoring;

internal static class MonitoringCalendar
{
    private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    public static DateOnly Day(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, Moscow));
}
