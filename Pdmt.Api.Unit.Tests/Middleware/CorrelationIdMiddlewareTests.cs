using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Pdmt.Api.Middleware;

namespace Pdmt.Api.Unit.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    private static async Task<HttpContext> Invoke(string? incomingId = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (incomingId is not null)
            ctx.Request.Headers["X-Correlation-Id"] = incomingId;

        var logger = Mock.Of<ILogger<CorrelationIdMiddleware>>();
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task InvokeAsync_NoHeader_GeneratesGuidAndSetsItemsAndResponseHeader()
    {
        var ctx = await Invoke();

        var correlationId = ctx.Items["CorrelationId"]?.ToString();
        correlationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(correlationId, out _).Should().BeTrue();

        ctx.Response.Headers["X-Correlation-Id"].ToString().Should().Be(correlationId);
    }

    [Fact]
    public async Task InvokeAsync_HeaderPresent_UsesExistingIdInItemsAndResponse()
    {
        var ctx = await Invoke("existing-correlation-id");

        ctx.Items["CorrelationId"].Should().Be("existing-correlation-id");
        ctx.Response.Headers["X-Correlation-Id"].ToString().Should().Be("existing-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_Always_SetsResponseHeader()
    {
        var ctxWithout = await Invoke();
        var ctxWith = await Invoke("some-id");

        ctxWithout.Response.Headers["X-Correlation-Id"].ToString().Should().NotBeNullOrEmpty();
        ctxWith.Response.Headers["X-Correlation-Id"].ToString().Should().NotBeNullOrEmpty();
    }
}
