namespace Pdmt.Api.Integration.Tests;

public class AnalyticsControllerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AnalyticsControllerTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }
}
