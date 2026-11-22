using EventContent.Api.Auth;
using EventContent.Application.Abstractions;
using EventContent.Domain.Common;
using EventContent.Domain.Organizations;

namespace EventContent.Api.Http;

/// <summary>
/// Resolves the tenant from the <c>organizationId</c> route value (or the <c>X-Organization-Id</c>
/// header for routes without one) and verifies membership server-side. Non-members get 404 rather
/// than 403 so organization ids cannot be probed.
/// </summary>
public sealed class TenantEndpointFilter(
    TenantContextAccessor tenant,
    CurrentUserAccessor currentUser,
    IMembershipLookup memberships) : IEndpointFilter
{
    public const string OrganizationRouteKey = "organizationId";
    public const string OrganizationHeader = "X-Organization-Id";

    private static readonly Error NotAMember = OrganizationErrors.NotFound;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!currentUser.IsSet)
        {
            return Results.Unauthorized();
        }

        var raw = http.Request.RouteValues.TryGetValue(OrganizationRouteKey, out var routeValue)
            ? routeValue?.ToString()
            : http.Request.Headers[OrganizationHeader].ToString();

        if (!Guid.TryParse(raw, out var organizationId))
        {
            return NotAMember.ToProblem(http);
        }

        var role = await memberships.FindRoleAsync(organizationId, currentUser.UserId, http.RequestAborted);
        if (role is null)
        {
            return NotAMember.ToProblem(http);
        }

        tenant.Set(organizationId, role.Value);
        return await next(context);
    }
}

/// <summary>Requires a permission in the resolved tenant. Must run after <see cref="TenantEndpointFilter"/>.</summary>
public sealed class PermissionEndpointFilter(Permission permission) : IEndpointFilter
{
    private static readonly Error MissingPermission =
        Error.Forbidden("auth.permission_denied", "Your role in this organization does not allow this action.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var tenant = context.HttpContext.RequestServices.GetRequiredService<TenantContextAccessor>();
        if (!tenant.IsResolved)
        {
            throw new InvalidOperationException("PermissionEndpointFilter requires TenantEndpointFilter to run first.");
        }

        return RolePermissions.Has(tenant.Role, permission)
            ? await next(context)
            : MissingPermission.ToProblem(context.HttpContext);
    }
}

public static class TenancyEndpointExtensions
{
    /// <summary>Marks a group as tenant-scoped: every endpoint requires membership of the organization.</summary>
    public static RouteGroupBuilder RequireTenant(this RouteGroupBuilder group) =>
        group.AddEndpointFilter<TenantEndpointFilter>();

    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new PermissionEndpointFilter(permission));
}
