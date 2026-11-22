using EventContent.Domain.Organizations;

namespace EventContent.UnitTests.Domain;

public sealed class RolePermissionsTests
{
    [Theory]
    [InlineData(OrganizationRole.Viewer, Permission.ContentRead, true)]
    [InlineData(OrganizationRole.Viewer, Permission.ContentEdit, false)]
    [InlineData(OrganizationRole.Viewer, Permission.ContentApprove, false)]
    [InlineData(OrganizationRole.Reviewer, Permission.ContentApprove, true)]
    [InlineData(OrganizationRole.Reviewer, Permission.ContentEdit, false)]
    [InlineData(OrganizationRole.Editor, Permission.ContentEdit, true)]
    [InlineData(OrganizationRole.Editor, Permission.ContentApprove, false)]
    [InlineData(OrganizationRole.Editor, Permission.MembersManage, false)]
    [InlineData(OrganizationRole.Admin, Permission.MembersManage, true)]
    [InlineData(OrganizationRole.Admin, Permission.IntegrationsManage, true)]
    public void Role_grants_expected_permissions(OrganizationRole role, Permission permission, bool expected) =>
        Assert.Equal(expected, RolePermissions.Has(role, permission));

    [Fact]
    public void Owner_has_every_permission()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.True(RolePermissions.Has(OrganizationRole.Owner, permission), permission.ToString());
        }
    }

    [Fact]
    public void Every_role_can_read_content()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            Assert.True(RolePermissions.Has(role, Permission.ContentRead), role.ToString());
        }
    }
}
