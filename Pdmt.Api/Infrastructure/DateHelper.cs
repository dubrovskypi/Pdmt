namespace Pdmt.Api.Infrastructure;

internal static class DateHelper
{
    /// <summary>
    /// Gets the Monday of the week containing the given date, in local timezone.
    /// </summary>
    internal static DateTimeOffset GetMonday(DateTimeOffset date, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(date.UtcDateTime, tz);
        var daysToSubtract = ((int)local.DayOfWeek + 6) % 7;
        var monday = local.Date.AddDays(-daysToSubtract);
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }

    /// <summary>
    /// Gets the first day of the month containing the given date, in local timezone.
    /// </summary>
    internal static DateTimeOffset GetFirstDayOfMonth(DateTimeOffset date, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(date.UtcDateTime, tz);
        var firstDay = new DateTime(local.Year, local.Month, 1);
        return new DateTimeOffset(firstDay, TimeSpan.Zero);
    }

    /// <summary>
    /// Legacy overload for backward compatibility with code that doesn't need timezone awareness.
    /// </summary>
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
