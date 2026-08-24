using System.Net;
using CleanArchitecture.Api.IntegrationTests.Configuration;
using Shouldly;

namespace CleanArchitecture.Api.IntegrationTests;

public sealed class HealthEndpointsTests(ApiTestFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task Live_WithoutAToken_ReportsHealthy()
    {
        using HttpResponseMessage response = await Client.GetAsync(Url(Routes.Health.Live), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_WithTheDatabaseUp_ReportsHealthy()
    {
        using HttpResponseMessage response = await Client.GetAsync(Url(Routes.Health.Ready), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }
}
