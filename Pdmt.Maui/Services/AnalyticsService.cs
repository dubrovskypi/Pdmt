using Pdmt.Maui.Models;
using System.Net.Http.Json;

namespace Pdmt.Maui.Services;

public class AnalyticsService(IHttpClientFactory factory)
{
    public async Task<CalendarWeekDto?> GetCalendarWeekAsync(DateTimeOffset weekOf)
    {
        var http = factory.CreateClient("PdmtApi");
        var param = Uri.EscapeDataString(weekOf.ToUniversalTime().ToString("yyyy-MM-dd"));
        return await http.GetFromJsonAsync<CalendarWeekDto>($"api/analytics/calendar/week?weekOf={param}");
    }

    public async Task<CalendarMonthDto?> GetCalendarMonthAsync(int year, int month)
    {
        var http = factory.CreateClient("PdmtApi");
        var param = $"{year:D4}-{month:D2}";
        return await http.GetFromJsonAsync<CalendarMonthDto>($"api/analytics/calendar/month?month={param}");
    }
}
