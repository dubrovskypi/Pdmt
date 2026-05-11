using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pdmt.Api.Data;
using Pdmt.Api.Dto;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Pdmt.Api.Integration.Tests.Controllers;

public class AuthControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    private readonly HttpClient _anonClient = factory.CreateClient();

    #region Register

    [Fact]
    public async Task Register_ValidData_Returns201()
    {
        var response = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = UniqueEmail(), Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email = UniqueEmail();
        await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        var response = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Login

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = UniqueEmail();
        await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        var response = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.AccessToken.Should().NotBeEmpty();
        body.RefreshToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = UniqueEmail();
        await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        var response = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = "wrongpassword" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = "nonexistent@pdmt-auth-test.com", Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokens()
    {
        var registerResponse = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = UniqueEmail(), Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        var response = await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = registered!.RefreshToken },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.AccessToken.Should().NotBeEmpty();
        body.RefreshToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Refresh_AlreadyUsedTokenWithinGraceWindow_Returns200()
    {
        var registerResponse = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = UniqueEmail(), Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = registered!.RefreshToken },
            TestContext.Current.CancellationToken);

        var response = await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = registered.RefreshToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var registerResponse = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = UniqueEmail(), Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(registered!.RefreshToken)));
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.RefreshTokens.SingleAsync(t => t.Token == tokenHash, TestContext.Current.CancellationToken);
            token.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = registered.RefreshToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout

    [Fact]
    public async Task Logout_ValidToken_Returns204()
    {
        var registerResponse = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = UniqueEmail(), Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        var response = await Client.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequestDto { RefreshToken = registered!.RefreshToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_DoesNotRevokeOtherDeviceToken()
    {
        var email = UniqueEmail();
        var loginA = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        // Register user and get two tokens
        await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        var loginResponse1 = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var session1 = await loginResponse1.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        var loginResponse2 = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var session2 = await loginResponse2.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        // Logout session1
        await Client.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequestDto { RefreshToken = session1!.RefreshToken },
            TestContext.Current.CancellationToken);

        // session2 should still work
        var refreshResponse = await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = session2!.RefreshToken },
            TestContext.Current.CancellationToken);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_Idempotent_SecondCallReturns204()
    {
        var registerResponse = await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = UniqueEmail(), Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);
        var dto = new RefreshRequestDto { RefreshToken = registered!.RefreshToken };

        await Client.PostAsJsonAsync("/api/auth/logout", dto, TestContext.Current.CancellationToken);
        var response = await Client.PostAsJsonAsync("/api/auth/logout", dto, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_Unauthenticated_Returns401()
    {
        var response = await _anonClient.PostAsJsonAsync("/api/auth/logout",
            new RefreshRequestDto { RefreshToken = "any-token" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region LogoutAll

    [Fact]
    public async Task LogoutAll_RevokesAllSessionsForUser()
    {
        var email = UniqueEmail();
        await _anonClient.PostAsJsonAsync("/api/auth/register",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);

        var loginResponse1 = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var session1 = await loginResponse1.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        var loginResponse2 = await _anonClient.PostAsJsonAsync("/api/auth/login",
            new UserDto { Email = email, Password = TestUserHelper.DefaultPassword },
            TestContext.Current.CancellationToken);
        var session2 = await loginResponse2.Content.ReadFromJsonAsync<AuthResultDto>(TestContext.Current.CancellationToken);

        var userClient = Factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new("Bearer", session1!.AccessToken);
        await userClient.PostAsync("/api/auth/logout-all", null, TestContext.Current.CancellationToken);

        var refresh1 = await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = session1!.RefreshToken },
            TestContext.Current.CancellationToken);
        var refresh2 = await _anonClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequestDto { RefreshToken = session2!.RefreshToken },
            TestContext.Current.CancellationToken);

        refresh1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        refresh2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutAll_Unauthenticated_Returns401()
    {
        var response = await _anonClient.PostAsync("/api/auth/logout-all", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    private static string UniqueEmail() => $"auth_{Guid.NewGuid():N}@test.com";
}
