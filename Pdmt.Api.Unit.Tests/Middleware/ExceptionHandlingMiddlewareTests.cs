using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Exceptions;
using Pdmt.Api.Middleware;

namespace Pdmt.Api.Unit.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static async Task<(int statusCode, ErrorResponse body)> InvokeWithException(
        Exception ex,
        string environment = "Production",
        string? correlationId = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (correlationId is not null)
            ctx.Items["CorrelationId"] = correlationId;

        var env = Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == environment);
        var logger = Mock.Of<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw ex, logger, env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = (await JsonSerializer.DeserializeAsync<ErrorResponse>(ctx.Response.Body, JsonOpts))!;
        return (ctx.Response.StatusCode, body);
    }

    public static TheoryData<Exception, int> KnownExceptions => new()
    {
        { new NotFoundException("not found"), 404 },
        { new UnauthorizedAccessException("unauthorized"), 401 },
        { new InvalidOperationException("invalid"), 400 },
        { new RateLimitExceededException("Auth.Login"), 429 },
    };

    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public async Task InvokeAsync_KnownException_ReturnsMappedStatusCode(Exception ex, int expectedStatus)
    {
        var (statusCode, _) = await InvokeWithException(ex);

        statusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        var (statusCode, _) = await InvokeWithException(new Exception("boom"));

        statusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_KnownException_InProduction_DetailsIsNull()
    {
        var (_, body) = await InvokeWithException(new NotFoundException("not found"), "Production");

        body.Details.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_KnownException_InDevelopment_DetailsContainsStackTrace()
    {
        Exception ex;
        try { throw new NotFoundException("not found"); }
        catch (NotFoundException e) { ex = e; }

        var (_, body) = await InvokeWithException(ex, "Development");

        body.Details.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_InDevelopment_DetailsIsAlwaysNull()
    {
        var (_, body) = await InvokeWithException(new Exception("boom"), "Development");

        body.Details.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_AnyException_SetsContentTypeApplicationJson()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        var env = Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == "Production");
        var logger = Mock.Of<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new NotFoundException("x"), logger, env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.ContentType.Should().StartWith("application/json");
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdInItems_IncludedInResponseBody()
    {
        var (_, body) = await InvokeWithException(
            new NotFoundException("not found"),
            correlationId: "test-correlation-id");

        body.CorrelationId.Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_NoCorrelationId_CorrelationIdIsNull()
    {
        var (_, body) = await InvokeWithException(new NotFoundException("not found"));

        body.CorrelationId.Should().BeNull();
    }
}
