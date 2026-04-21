using Pdmt.Api.Dto;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pdmt.Api.Integration.Tests;

public class WebAuthControllerTests(WebAuthWebAppFactory factory) : IClassFixture<WebAuthWebAppFactory>
{

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidCredentials_Returns201()
    {
        var client = factory.CreateClient();
        var dto = new UserDto { Email = UniqueEmail(), Password = "password123" };

        var response = await client.PostAsJsonAsync("/api/auth/web/register", dto, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ValidCredentials_SetsHttpOnlyCookie()
    {
        var client = factory.CreateClient();
        var dto = new UserDto { Email = UniqueEmail(), Password = "password123" };

        var response = await client.PostAsJsonAsync("/api/auth/web/register", dto, TestContext.Current.CancellationToken);

        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookie);
        Assert.Contains("refreshToken=", setCookie);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ValidCredentials_DoesNotExposeRefreshTokenInBody()
    {
        var client = factory.CreateClient();
        var dto = new UserDto { Email = UniqueEmail(), Password = "password123" };

        var response = await client.PostAsJsonAsync("/api/auth/web/register", dto, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WebAuthResultDto>(TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_EmptyEmail_Returns400()
    {
        var client = factory.CreateClient();
        var dto = new UserDto { Email = "", Password = "password123" };

        var response = await client.PostAsJsonAsync("/api/auth/web/register", dto, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_SetsRefreshCookie()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);

        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookie);
        Assert.Contains("refreshToken=", setCookie);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = email, Password = "wrongpassword" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_WithCookie_Returns200AndNewAccessToken()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);
        var refreshCookie = ExtractRefreshCookie(registerResponse);

        client.DefaultRequestHeaders.Add("Cookie", $"refreshToken={refreshCookie}");
        var response = await client.PostAsync("/api/auth/web/refresh", null, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WebAuthResultDto>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesRefreshCookie()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = email, Password = "password123" }, TestContext.Current.CancellationToken);
        var oldToken = ExtractRefreshCookie(registerResponse);

        client.DefaultRequestHeaders.Add("Cookie", $"refreshToken={oldToken}");
        var refreshResponse = await client.PostAsync("/api/auth/web/refresh", null, TestContext.Current.CancellationToken);
        var newToken = ExtractRefreshCookie(refreshResponse);

        Assert.NotNull(newToken);
        Assert.NotEqual(oldToken, newToken);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/web/refresh", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Authenticated_Returns204()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");

        var response = await client.PostAsync("/api/auth/web/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_Authenticated_ClearsRefreshCookie()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestScheme");

        var response = await client.PostAsync("/api/auth/web/logout", null, TestContext.Current.CancellationToken);
        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault();

        Assert.NotNull(setCookie);
        Assert.Contains("refreshToken=;", setCookie);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string UniqueEmail() => $"web_{Guid.NewGuid():N}@test.com";

    private static string? ExtractRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault(h => h.Contains("refreshToken="));
        if (setCookie is null) return null;
        var start = setCookie.IndexOf("refreshToken=", StringComparison.Ordinal) + "refreshToken=".Length;
        var end = setCookie.IndexOf(';', start);
        return end == -1 ? setCookie[start..] : setCookie[start..end];
    }
}
