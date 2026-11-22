using EventContent.Application.Abstractions;
using EventContent.Application.Common;
using EventContent.Contracts.Organizations;
using EventContent.Domain.Common;
using EventContent.Domain.Organizations;

namespace EventContent.Application.Organizations;

public sealed class GetOrganizationHandler(IOrganizationRepository organizations, ITenantContext tenant)
{
    public async Task<Result<OrganizationResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var organization = await organizations.GetWithMembersAsync(tenant.OrganizationId, cancellationToken);
        return organization is null
            ? OrganizationErrors.NotFound
            : organization.ToResponse(tenant.Role);
    }
}

public sealed class UpdateOrganizationHandler(
    IOrganizationRepository organizations,
    ITenantContext tenant,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<OrganizationResponse>> HandleAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organization = await organizations.GetWithMembersAsync(tenant.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        if (organization.Version != request.Version)
        {
            return CommonErrors.VersionConflict;
        }

        var before = new { organization.Name, organization.DefaultTimeZone };
        var updated = organization.Update(request.Name, request.DefaultTimeZone);
        if (!updated.IsSuccess)
        {
            return updated.Error;
        }

        auditLog.Record(organization.Id, "Organization", organization.Id, "organization.updated", new
        {
            before,
            after = new { organization.Name, organization.DefaultTimeZone },
        });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return CommonErrors.VersionConflict;
        }

        return organization.ToResponse(tenant.Role);
    }
}
