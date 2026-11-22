using EventContent.Application.Audit;
using EventContent.Application.Organizations;
using EventContent.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace EventContent.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<GetOrganizationHandler>();
        services.AddScoped<UpdateOrganizationHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<AddMemberHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<ListAuditLogHandler>();
        return services;
    }
}
