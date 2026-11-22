using System.Security.Claims;
using EventContent.Application.Abstractions;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;

namespace EventContent.Api.Auth;

/// <summary>Per-request user, set by <see cref="UserProvisioningMiddleware"/>.</summary>
public sealed class CurrentUserAccessor : ICurrentUser
{
    private User? _user;

    public bool IsSet => _user is not null;

    public Guid UserId => User.Id;

    public string Email => User.Email;

    public string DisplayName => User.DisplayName;

    private User User => _user ?? throw new InvalidOperationException("No authenticated user for this request.");

    public void Set(User user) => _user = user;
}

/// <summary>Per-request tenant, set by <see cref="TenantEndpointFilter"/> after a membership check.</summary>
public sealed class TenantContextAccessor : ITenantContext
{
    private (Guid OrganizationId, OrganizationRole Role)? _tenant;

    public bool IsResolved => _tenant.HasValue;

    public Guid OrganizationId => Tenant.OrganizationId;

    public OrganizationRole Role => Tenant.Role;

    private (Guid OrganizationId, OrganizationRole Role) Tenant =>
        _tenant ?? throw new InvalidOperationException("No tenant has been resolved for this request.");

    public void Set(Guid organizationId, OrganizationRole role)
    {
        if (_tenant.HasValue && _tenant.Value.OrganizationId != organizationId)
        {
            throw new InvalidOperationException("The tenant cannot change within a request.");
        }

        _tenant = (organizationId, role);
    }
}

/// <summary>
/// Maps the authenticated principal to an application user, creating it on first sign-in
/// (just-in-time provisioning).
/// </summary>
public sealed class UserProvisioningMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan LastSeenResolution = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        HttpContext context,
        CurrentUserAccessor currentUser,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var issuer = principal.FindFirstValue("iss");
            var subject = principal.FindFirstValue("sub");
            if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var now = clock.GetUtcNow();
            var ct = context.RequestAborted;

            var user = await users.FindByIdentityAsync(issuer, subject, ct);
            if (user is null)
            {
                user = await users.ProvisionAsync(User.Provision(issuer, subject, email, name, now), ct);
            }
            else if (now - user.LastSeenAt > LastSeenResolution)
            {
                user.RecordSignIn(email, name, now);
                await unitOfWork.SaveChangesAsync(ct);
            }

            currentUser.Set(user);
        }

        await next(context);
    }
}
