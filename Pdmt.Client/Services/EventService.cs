using Microsoft.Extensions.Options;
using Pdmt.Client.Configuration;
using Pdmt.Client.Models;
using System.Net.Http.Json;

namespace Pdmt.Client.Services
{
    public class EventService
    {
        private readonly HttpClient _http;

        public EventService(IHttpClientFactory factory, IOptions<PdmtApiOptions> options)
        {
            _http = factory.CreateClient(options.Value.ClientName);
        }

        public async Task<List<EventResponseDto>> GetEventsAsync(
            DateTime? from = null, DateTime? to = null,
            int? type = null, IReadOnlyList<Guid>? tagIds = null,
            int? minIntensity = null, int? maxIntensity = null)
        {
            var query = new List<string>();
            if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            if (type.HasValue) query.Add($"type={type.Value}");
            if (tagIds is not null && tagIds.Count > 0) query.Add($"tags={string.Join(",", tagIds)}");
            if (minIntensity.HasValue) query.Add($"minIntensity={minIntensity.Value}");
            if (maxIntensity.HasValue) query.Add($"maxIntensity={maxIntensity.Value}");

            var url = query.Count > 0 ? $"api/events?{string.Join("&", query)}" : "api/events";
            return await _http.GetFromJsonAsync<List<EventResponseDto>>(url) ?? [];
        }

        public async Task<EventResponseDto?> GetByIdAsync(Guid id)
        {
            return await _http.GetFromJsonAsync<EventResponseDto>($"api/events/{id}");
        }

        public async Task<EventResponseDto> CreateEventAsync(CreateEventDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/events", dto);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EventResponseDto>())!;
        }

        public async Task UpdateEventAsync(Guid id, UpdateEventDto dto)
        {
            var response = await _http.PutAsJsonAsync($"api/events/{id}", dto);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteEventAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/events/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
