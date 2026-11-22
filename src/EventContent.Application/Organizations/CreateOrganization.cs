using EventContent.Application.Abstractions;
using EventContent.Contracts.Organizations;
using EventContent.Domain.Common;
using EventContent.Domain.Organizations;

namespace EventContent.Application.Organizations;

public sealed class CreateOrganizationHandler(
    IOrganizationRepository organizations,
    ICurrentUser currentUser,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<OrganizationResponse>> HandleAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var created = Organization.Create(request.Name, request.Slug, request.DefaultTimeZone, currentUser.UserId, clock.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        var organization = created.Value;
        if (await organizations.SlugExistsAsync(organization.Slug, cancellationToken))
        {
            return OrganizationErrors.SlugTaken;
        }

        organizations.Add(organization);
        auditLog.Record(organization.Id, "Organization", organization.Id, "organization.created", new
        {
            organization.Name,
            organization.Slug,
            organization.DefaultTimeZone,
        });

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Another request claimed the slug between the check and the insert.
            return OrganizationErrors.SlugTaken;
        }

        return organization.ToResponse(OrganizationRole.Owner);
    }
}
