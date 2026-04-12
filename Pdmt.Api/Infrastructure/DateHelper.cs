namespace Pdmt.Api.Infrastructure;

internal static class DateHelper
{
    internal static DateTimeOffset GetMonday(DateTimeOffset date)
    {
        var daysToSubtract = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.Date.AddDays(-daysToSubtract);
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }

    internal static DateTimeOffset GetMonday(DateOnly date) =>
        GetMonday(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), TimeSpan.Zero));

    internal static DateOnly ToLocalDate(DateTimeOffset utcTimestamp, TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp.UtcDateTime, tz));
}
