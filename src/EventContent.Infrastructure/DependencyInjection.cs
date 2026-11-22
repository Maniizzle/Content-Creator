using EventContent.Application.Abstractions;
using EventContent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventContent.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "Database";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{DatabaseConnectionName}' is not configured. See .env.example.");
        }

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll));

        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMembershipLookup, MembershipLookup>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        return services;
    }
}
