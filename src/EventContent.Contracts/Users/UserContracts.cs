using EventContent.Contracts.Organizations;

namespace EventContent.Contracts.Users;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<OrganizationSummaryResponse> Organizations);
