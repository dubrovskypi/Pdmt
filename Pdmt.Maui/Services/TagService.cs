using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class TagService(IHttpClientFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient("PdmtApi");

    public async Task<List<TagResponseDto>> GetTagsAsync()
    {
        var result = await _http.GetFromJsonAsync<List<TagResponseDto>>("api/tags");
        return result ?? [];
    }
}
