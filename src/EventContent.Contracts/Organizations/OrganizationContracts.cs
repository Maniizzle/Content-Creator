namespace EventContent.Contracts.Organizations;

public sealed record CreateOrganizationRequest(string Name, string Slug, string DefaultTimeZone);

public sealed record UpdateOrganizationRequest(string Name, string DefaultTimeZone, uint Version);

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    string DefaultTimeZone,
    string MyRole,
    IReadOnlyList<string> MyPermissions,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record OrganizationSummaryResponse(Guid Id, string Name, string Slug, string MyRole);

public sealed record AddMemberRequest(string Email, string Role);

public sealed record ChangeMemberRoleRequest(string Role);

public sealed record MemberResponse(Guid Id, Guid UserId, string Email, string DisplayName, string Role, DateTimeOffset JoinedAt);
