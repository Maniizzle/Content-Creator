using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EventContent.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
#pragma warning disable CA1031 // A health check must report failure, not throw.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Do not include the exception message: it can contain host names or connection details.
            return HealthCheckResult.Unhealthy("Database check failed.", exception: null, data: new Dictionary<string, object>
            {
                ["error"] = ex.GetType().Name,
            });
        }
    }
}
