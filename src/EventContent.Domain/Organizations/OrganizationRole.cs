namespace EventContent.Domain.Organizations;

/// <summary>Role of a member within one organization. Stored as text; do not rename members.</summary>
public enum OrganizationRole
{
    Viewer,
    Reviewer,
    Editor,
    Admin,
    Owner,
}

/// <summary>Fine-grained capabilities. Endpoints authorize against permissions, never roles directly.</summary>
public enum Permission
{
    ContentRead,
    ContentEdit,
    ContentApprove,
    MembersManage,
    OrganizationManage,
    IntegrationsManage,
}

public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<OrganizationRole, HashSet<Permission>> Map =
        new Dictionary<OrganizationRole, HashSet<Permission>>
        {
            [OrganizationRole.Viewer] = [Permission.ContentRead],
            [OrganizationRole.Reviewer] = [Permission.ContentRead, Permission.ContentApprove],
            [OrganizationRole.Editor] = [Permission.ContentRead, Permission.ContentEdit],
            [OrganizationRole.Admin] =
            [
                Permission.ContentRead, Permission.ContentEdit, Permission.ContentApprove,
                Permission.MembersManage, Permission.OrganizationManage, Permission.IntegrationsManage,
            ],
            [OrganizationRole.Owner] = [.. Enum.GetValues<Permission>()],
        };

    public static bool Has(OrganizationRole role, Permission permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static IReadOnlyCollection<Permission> For(OrganizationRole role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : [];
}
