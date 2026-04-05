using Microsoft.Extensions.Options;
using Pdmt.Client.Configuration;
using Pdmt.Client.Models;
using System.Net.Http.Json;

namespace Pdmt.Client.Services
{
    public class AnalyticsService(IHttpClientFactory factory, IOptions<PdmtApiOptions> options)
    {
        private readonly HttpClient _http = factory.CreateClient(options.Value.ClientName);

        public async Task<CalendarWeekDto?> GetCalendarWeekAsync(DateOnly weekOf)
        {
            var param = Uri.EscapeDataString(weekOf.ToString("yyyy-MM-dd"));
            return await _http.GetFromJsonAsync<CalendarWeekDto>($"api/analytics/calendar/week?weekOf={param}");
        }
    }
}
