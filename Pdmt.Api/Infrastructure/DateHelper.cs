namespace Pdmt.Api.Infrastructure;

internal static class DateHelper
{
    /// <summary>
    /// Gets the Monday of the week containing the given UTC timestamp, as local midnight with correct UTC offset.
    /// </summary>
    internal static DateTimeOffset GetMonday(DateTimeOffset date, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(date.UtcDateTime, tz);
        var daysToSubtract = ((int)local.DayOfWeek + 6) % 7;
        var monday = local.Date.AddDays(-daysToSubtract);
        return new DateTimeOffset(monday, tz.GetUtcOffset(monday));
    }

    /// <summary>
    /// Gets the first day of the month containing the given UTC timestamp, as local midnight with correct UTC offset.
    /// </summary>
    internal static DateTimeOffset GetFirstDayOfMonth(DateTimeOffset date, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(date.UtcDateTime, tz);
        var firstDay = new DateTime(local.Year, local.Month, 1);
        return new DateTimeOffset(firstDay, tz.GetUtcOffset(firstDay));
    }

    /// <summary>
    /// Gets the Monday of the week containing the given local date.
    /// Returns UTC (offset=0) so it is safe to use in EF WHERE clauses against timestamptz.
    /// </summary>
    internal static DateTimeOffset GetMonday(DateOnly date, TimeZoneInfo tz)
    {
        var daysToSubtract = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.AddDays(-daysToSubtract).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(monday, tz), TimeSpan.Zero);
    }

    internal static DateOnly ToLocalDate(DateTimeOffset utcTimestamp, TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp.UtcDateTime, tz));
}
