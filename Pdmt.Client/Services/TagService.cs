using Microsoft.Extensions.Options;
using Pdmt.Client.Configuration;
using Pdmt.Client.Models;
using System.Net.Http.Json;

namespace Pdmt.Client.Services
{
    public class TagService
    {
        private readonly HttpClient _http;

        public TagService(IHttpClientFactory factory, IOptions<PdmtApiOptions> options)
        {
            _http = factory.CreateClient(options.Value.ClientName);
        }

        public async Task<List<TagResponseDto>> GetTagsAsync()
        {
            return await _http.GetFromJsonAsync<List<TagResponseDto>>("api/tags") ?? [];
        }

        public async Task DeleteTagAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/tags/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
