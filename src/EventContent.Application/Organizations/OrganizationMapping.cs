using EventContent.Contracts.Organizations;
using EventContent.Domain.Organizations;

namespace EventContent.Application.Organizations;

internal static class OrganizationMapping
{
    public static OrganizationResponse ToResponse(this Organization organization, OrganizationRole myRole) =>
        new(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.DefaultTimeZone,
            myRole.ToString(),
            [.. RolePermissions.For(myRole).Select(p => p.ToString()).Order(StringComparer.Ordinal)],
            organization.CreatedAt,
            organization.Version);

    public static OrganizationSummaryResponse ToSummary(this Organization organization, OrganizationRole myRole) =>
        new(organization.Id, organization.Name, organization.Slug, myRole.ToString());

    /// <summary>
    /// Parses a role name strictly. <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> alone would
    /// also accept numeric strings such as "4", which must not be a way to request the Owner role.
    /// </summary>
    public static bool TryParseRole(string? value, out OrganizationRole role)
    {
        role = default;
        return !string.IsNullOrWhiteSpace(value)
            && value.All(char.IsLetter)
            && Enum.TryParse(value, ignoreCase: true, out role)
            && Enum.IsDefined(role);
    }
}
