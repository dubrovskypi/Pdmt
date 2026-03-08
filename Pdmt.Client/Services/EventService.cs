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

        public async Task<List<EventResponseDto>> GetEventsAsync()
        {
            return await _http.GetFromJsonAsync<List<EventResponseDto>>("api/events") ?? [];
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
