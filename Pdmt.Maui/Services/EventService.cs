using System.Net.Http.Json;
using System.Text;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class EventService(IHttpClientFactory factory)
{
    public async Task<List<EventResponseDto>> GetEventsAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? type = null,
        IReadOnlyList<Guid>? tagIds = null,
        int? minIntensity = null,
        int? maxIntensity = null)
    {
        var query = new StringBuilder("api/events?");
        if (from.HasValue) query.Append($"from={DateTime.SpecifyKind(from.Value, DateTimeKind.Utc):O}&");
        if (to.HasValue) query.Append($"to={DateTime.SpecifyKind(to.Value, DateTimeKind.Utc):O}&");
        if (type.HasValue) query.Append($"type={type.Value}&");
        if (tagIds is { Count: > 0 }) query.Append($"tags={string.Join(",", tagIds)}&");
        if (minIntensity.HasValue) query.Append($"minIntensity={minIntensity.Value}&");
        if (maxIntensity.HasValue) query.Append($"maxIntensity={maxIntensity.Value}&");

        var http = factory.CreateClient("PdmtApi");
        var result = await http.GetFromJsonAsync<List<EventResponseDto>>(query.ToString());
        return result ?? [];
    }

    public async Task<EventResponseDto> CreateEventAsync(CreateEventDto dto)
    {
        var http = factory.CreateClient("PdmtApi");
        var response = await http.PostAsJsonAsync("api/events", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponseDto>())!;
    }

    public async Task DeleteEventAsync(Guid id)
    {
        var http = factory.CreateClient("PdmtApi");
        var response = await http.DeleteAsync($"api/events/{id}");
        response.EnsureSuccessStatusCode();
    }
}
