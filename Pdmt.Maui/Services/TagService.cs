using System.Net.Http.Json;
using Pdmt.Maui.Models;

namespace Pdmt.Maui.Services;

public class TagService(IHttpClientFactory factory)
{
    public async Task<List<TagResponseDto>> GetTagsAsync()
    {
        var http = factory.CreateClient("PdmtApi");
        var result = await http.GetFromJsonAsync<List<TagResponseDto>>("api/tags");
        return result ?? [];
    }
}
