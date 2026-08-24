using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CleanArchitecture.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The database is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The database is not reachable.", exception);
        }
    }
}
