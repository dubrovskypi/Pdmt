using Microsoft.JSInterop;

namespace Pdmt.Web.Services
{
    public class TokenService
    {
        private readonly IJSRuntime _js;

        public TokenService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task SetTokenAsync(string token)
            => await _js.InvokeVoidAsync("localStorage.setItem", "access_token", token);

        public async Task<string?> GetTokenAsync()
            => await _js.InvokeAsync<string?>("localStorage.getItem", "access_token");

        public async Task RemoveTokenAsync()
            => await _js.InvokeVoidAsync("localStorage.removeItem", "access_token");
    }
}
