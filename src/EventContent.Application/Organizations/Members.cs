using EventContent.Application.Abstractions;
using EventContent.Application.Common;
using EventContent.Contracts.Organizations;
using EventContent.Domain.Common;
using EventContent.Domain.Organizations;
using EventContent.Domain.Users;

namespace EventContent.Application.Organizations;

public sealed class ListMembersHandler(IOrganizationQueries queries, ITenantContext tenant)
{
    public async Task<IReadOnlyList<MemberResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var members = await queries.ListMembersAsync(tenant.OrganizationId, cancellationToken);
        return [.. members.Select(m => m.ToResponse())];
    }
}

public sealed class AddMemberHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    ITenantContext tenant,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<MemberResponse>> HandleAsync(AddMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!OrganizationMapping.TryParseRole(request.Role, out var role))
        {
            return CommonErrors.InvalidField("role", "Role must be one of Owner, Admin, Editor, Reviewer or Viewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 320 || !request.Email.Contains('@', StringComparison.Ordinal))
        {
            return CommonErrors.InvalidField("email", "Enter a valid e-mail address.");
        }

        var organization = await organizations.GetWithMembersAsync(tenant.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        // MVP: members must have signed in once. E-mail invitations come with real authentication.
        var user = await users.FindByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user is null)
        {
            return OrganizationErrors.UserNotFound;
        }

        var added = organization.AddMember(user.Id, role, tenant.Role, clock.GetUtcNow());
        if (!added.IsSuccess)
        {
            return added.Error;
        }

        var member = added.Value;
        auditLog.Record(organization.Id, "OrganizationMember", member.Id, "member.added", new { member.UserId, Role = role.ToString() });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return OrganizationErrors.AlreadyMember;
        }
        catch (ConcurrencyConflictException)
        {
            return CommonErrors.VersionConflict;
        }

        return new MemberView(member, user.Email, user.DisplayName).ToResponse();
    }
}

public sealed class ChangeMemberRoleHandler(
    IOrganizationRepository organizations,
    ITenantContext tenant,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid memberId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!OrganizationMapping.TryParseRole(request.Role, out var role))
        {
            return CommonErrors.InvalidField("role", "Role must be one of Owner, Admin, Editor, Reviewer or Viewer.");
        }

        var organization = await organizations.GetWithMembersAsync(tenant.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var previousRole = organization.Members.SingleOrDefault(m => m.Id == memberId)?.Role;
        var changed = organization.ChangeMemberRole(memberId, role, tenant.Role);
        if (!changed.IsSuccess)
        {
            return changed;
        }

        if (previousRole == role)
        {
            return Result.Success();
        }

        auditLog.Record(organization.Id, "OrganizationMember", memberId, "member.role_changed", new
        {
            from = previousRole?.ToString(),
            to = role.ToString(),
        });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return CommonErrors.VersionConflict;
        }

        return Result.Success();
    }
}

public sealed class RemoveMemberHandler(
    IOrganizationRepository organizations,
    ITenantContext tenant,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetWithMembersAsync(tenant.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var removed = organization.RemoveMember(memberId, tenant.Role);
        if (!removed.IsSuccess)
        {
            return removed.Error;
        }

        auditLog.Record(organization.Id, "OrganizationMember", memberId, "member.removed", new
        {
            removed.Value.UserId,
            Role = removed.Value.Role.ToString(),
        });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return CommonErrors.VersionConflict;
        }

        return Result.Success();
    }
}

internal static class MemberMapping
{
    public static MemberResponse ToResponse(this MemberView view) =>
        new(view.Member.Id, view.Member.UserId, view.Email, view.DisplayName, view.Member.Role.ToString(), view.Member.CreatedAt);
}
