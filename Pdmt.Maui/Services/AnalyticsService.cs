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
}
