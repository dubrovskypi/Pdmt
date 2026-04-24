using FluentAssertions;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pdmt.Api.Integration.Tests;

public class WebAuthControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    #region Register

    [Fact]
    public async Task Register_ValidData_Returns201()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = UniqueEmail(), Password = "Password123!" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_ValidData_SetsHttpOnlyCookie()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = UniqueEmail(), Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault(h => h.Contains("refreshToken="));

        setCookie.Should().NotBeNull()
            .And.Contain("refreshToken=")
            .And.ContainEquivalentOf("httponly");
    }

    [Fact]
    public async Task Register_ValidData_RefreshTokenNotInBody()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = UniqueEmail(), Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().NotContainEquivalentOf("refreshToken");
    }

    [Fact]
    public async Task Register_EmptyEmail_Returns400()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/register",
            new UserDto { Email = "", Password = "Password123!" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Login

    [Fact]
    public async Task Login_ValidCredentials_Returns200()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = "test@pdmt.dev", Password = "Password123!" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_ValidCredentials_SetsHttpOnlyCookie()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = "test@pdmt.dev", Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault(h => h.Contains("refreshToken="));

        setCookie.Should().NotBeNull()
            .And.Contain("refreshToken=")
            .And.ContainEquivalentOf("httponly");
    }

    [Fact]
    public async Task Login_ValidCredentials_RefreshTokenNotInBody()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = "test@pdmt.dev", Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().NotContainEquivalentOf("refreshToken");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = "test@pdmt.dev", Password = "wrongpassword" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task Refresh_WithValidCookie_Returns200()
    {
        var loginResponse = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = "test@pdmt.dev", Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var cookie = ExtractRefreshCookie(loginResponse);

        var refreshClient = Factory.CreateClient();
        refreshClient.DefaultRequestHeaders.Add("Cookie", $"refreshToken={cookie}");
        var response = await refreshClient.PostAsync("/api/auth/web/refresh", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WebAuthResultDto>(TestContext.Current.CancellationToken);
        body!.AccessToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesRefreshToken()
    {
        var loginResponse = await Factory.CreateClient().PostAsJsonAsync("/api/auth/web/login",
            new UserDto { Email = "test@pdmt.dev", Password = "Password123!" },
            TestContext.Current.CancellationToken);
        var oldCookie = ExtractRefreshCookie(loginResponse);

        var refreshClient = Factory.CreateClient();
        refreshClient.DefaultRequestHeaders.Add("Cookie", $"refreshToken={oldCookie}");
        var refreshResponse = await refreshClient.PostAsync("/api/auth/web/refresh", null,
            TestContext.Current.CancellationToken);
        var newCookie = ExtractRefreshCookie(refreshResponse);

        newCookie.Should().NotBeNull().And.NotBe(oldCookie);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var response = await Factory.CreateClient().PostAsync("/api/auth/web/refresh", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout

    [Fact]
    public async Task Logout_Authenticated_Returns204()
    {
        var response = await Client.PostAsync("/api/auth/web/logout", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_ClearsRefreshCookie()
    {
        var authClient = Factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName);

        var response = await authClient.PostAsync("/api/auth/web/logout", null,
            TestContext.Current.CancellationToken);
        var setCookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault(h => h.Contains("refreshToken"));

        setCookie.Should().NotBeNull().And.Contain("refreshToken=;");
    }

    #endregion

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
