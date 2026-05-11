using FluentAssertions;
using Pdmt.Api.Integration.Tests.Infrastructure;
using System.Net;

namespace Pdmt.Api.Integration.Tests.Controllers;

public class HealthControllerTests(PostgresWebAppFactory factory) : HttpTestBase(factory)
{
    #region LivenessEndpoint

    [Fact]
    public async Task LivenessEndpoint_Returns200()
    {
        var response = await Client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region ReadinessEndpoint

    [Fact]
    public async Task ReadinessEndpoint_Returns200()
    {
        var response = await Client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region HealthAlias

    [Fact]
    public async Task HealthAlias_Returns200()
    {
        var response = await Client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
