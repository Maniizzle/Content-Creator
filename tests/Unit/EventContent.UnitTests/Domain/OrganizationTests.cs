using EventContent.Domain.Common;
using EventContent.Domain.Organizations;

namespace EventContent.UnitTests.Domain;

public sealed class OrganizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Creator = Guid.NewGuid();

    private static Organization NewOrganization() =>
        Organization.Create("JCI Lagos", "jci-lagos", "Africa/Lagos", Creator, Now).Value;

    [Fact]
    public void Create_makes_the_creator_the_only_owner()
    {
        var organization = NewOrganization();

        var member = Assert.Single(organization.Members);
        Assert.Equal(Creator, member.UserId);
        Assert.Equal(OrganizationRole.Owner, member.Role);
        Assert.Equal(organization.Id, member.OrganizationId);
    }

    [Fact]
    public void Create_normalizes_name_and_slug()
    {
        var organization = Organization.Create("  JCI Lagos  ", " JCI-Lagos ", "Africa/Lagos", Creator, Now).Value;

        Assert.Equal("JCI Lagos", organization.Name);
        Assert.Equal("jci-lagos", organization.Slug);
    }

    [Theory]
    [InlineData("a", "valid-slug", "Africa/Lagos", "name")]
    [InlineData("Valid", "-bad", "Africa/Lagos", "slug")]
    [InlineData("Valid", "bad--slug", "Africa/Lagos", "slug")]
    [InlineData("Valid", "ab", "Africa/Lagos", "slug")]
    [InlineData("Valid", "has space", "Africa/Lagos", "slug")]
    [InlineData("Valid", "valid-slug", "Mars/Olympus", "defaultTimeZone")]
    [InlineData("Valid", "valid-slug", "", "defaultTimeZone")]
    public void Create_rejects_invalid_input(string name, string slug, string timeZone, string field)
    {
        var result = Organization.Create(name, slug, timeZone, Creator, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.NotNull(result.Error.FieldErrors);
        Assert.True(result.Error.FieldErrors.ContainsKey(field));
    }

    [Fact]
    public void Admin_cannot_grant_owner_role()
    {
        var organization = NewOrganization();

        var result = organization.AddMember(Guid.NewGuid(), OrganizationRole.Owner, OrganizationRole.Admin, Now);

        Assert.Equal(OrganizationErrors.OwnerRoleRequired, result.Error);
    }

    [Fact]
    public void Adding_the_same_user_twice_is_a_conflict()
    {
        var organization = NewOrganization();
        var user = Guid.NewGuid();
        organization.AddMember(user, OrganizationRole.Editor, OrganizationRole.Owner, Now);

        var result = organization.AddMember(user, OrganizationRole.Viewer, OrganizationRole.Owner, Now);

        Assert.Equal(OrganizationErrors.AlreadyMember, result.Error);
    }

    [Fact]
    public void The_last_owner_cannot_be_demoted_or_removed()
    {
        var organization = NewOrganization();
        var owner = organization.Members.Single();

        Assert.Equal(OrganizationErrors.LastOwner, organization.ChangeMemberRole(owner.Id, OrganizationRole.Admin, OrganizationRole.Owner).Error);
        Assert.Equal(OrganizationErrors.LastOwner, organization.RemoveMember(owner.Id, OrganizationRole.Owner).Error);
    }

    [Fact]
    public void An_owner_can_step_down_once_another_owner_exists()
    {
        var organization = NewOrganization();
        var original = organization.Members.Single();
        organization.AddMember(Guid.NewGuid(), OrganizationRole.Owner, OrganizationRole.Owner, Now);

        var result = organization.ChangeMemberRole(original.Id, OrganizationRole.Editor, OrganizationRole.Owner);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationRole.Editor, original.Role);
    }

    [Fact]
    public void Admin_cannot_modify_or_remove_an_owner()
    {
        var organization = NewOrganization();
        var owner = organization.Members.Single();
        organization.AddMember(Guid.NewGuid(), OrganizationRole.Owner, OrganizationRole.Owner, Now);

        Assert.Equal(OrganizationErrors.OwnerRoleRequired, organization.ChangeMemberRole(owner.Id, OrganizationRole.Viewer, OrganizationRole.Admin).Error);
        Assert.Equal(OrganizationErrors.OwnerRoleRequired, organization.RemoveMember(owner.Id, OrganizationRole.Admin).Error);
    }

    [Fact]
    public void Membership_changes_bump_the_revision()
    {
        var organization = NewOrganization();
        var before = organization.MembershipRevision;

        var member = organization.AddMember(Guid.NewGuid(), OrganizationRole.Viewer, OrganizationRole.Admin, Now).Value;
        organization.ChangeMemberRole(member.Id, OrganizationRole.Editor, OrganizationRole.Admin);
        organization.RemoveMember(member.Id, OrganizationRole.Admin);

        Assert.Equal(before + 3, organization.MembershipRevision);
    }

    [Fact]
    public void Unknown_member_is_not_found()
    {
        var organization = NewOrganization();

        Assert.Equal(OrganizationErrors.MemberNotFound, organization.RemoveMember(Guid.NewGuid(), OrganizationRole.Owner).Error);
    }
}
