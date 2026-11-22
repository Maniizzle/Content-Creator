using EventContent.Application.Abstractions;
using EventContent.Application.Organizations;
using EventContent.Contracts.Users;

namespace EventContent.Application.Users;

public sealed class GetCurrentUserHandler(ICurrentUser currentUser, IOrganizationQueries queries)
{
    public async Task<CurrentUserResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var memberships = await queries.ListForUserAsync(currentUser.UserId, cancellationToken);
        return new CurrentUserResponse(
            currentUser.UserId,
            currentUser.Email,
            currentUser.DisplayName,
            [.. memberships.Select(m => m.Organization.ToSummary(m.Role))]);
    }
}
