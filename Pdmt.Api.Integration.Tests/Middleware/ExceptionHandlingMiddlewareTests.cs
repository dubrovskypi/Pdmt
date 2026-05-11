using FluentAssertions;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pdmt.Api.Integration.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    #region NotFoundException

    [Fact]
    public async Task NotFoundException_Returns404WithJsonBody()
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await Client.GetAsync(
            $"/api/analytics/correlations?tagId={Guid.NewGuid()}&from={from}&to={to}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ValidationException

    [Fact]
    public async Task ValidationException_DuplicateRegister_Returns400WithJsonBody()
    {
        var dto = new { Email = "dup@example.com", Password = "password123" };
        await Client.PostAsJsonAsync("/api/auth/register", dto, TestContext.Current.CancellationToken);

        var response = await Client.PostAsJsonAsync("/api/auth/register", dto, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region UnauthorizedAccessException

    [Fact]
    public async Task UnauthorizedAccessException_Returns401WithJsonBody()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new { Email = "login@example.com", Password = "correct123" }, TestContext.Current.CancellationToken);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = "login@example.com", Password = "wrongpassword" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion
}
