using EventContent.Domain.Common;

namespace EventContent.Domain.Organizations;

public static class OrganizationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("organization.not_found", "The organization does not exist or you are not a member.");

    public static readonly Error SlugTaken =
        Error.Conflict("organization.slug_taken", "That organization URL name is already in use.");

    public static readonly Error MemberNotFound =
        Error.NotFound("organization.member_not_found", "The member does not exist in this organization.");

    public static readonly Error AlreadyMember =
        Error.Conflict("organization.already_member", "That user is already a member of this organization.");

    public static readonly Error UserNotFound =
        Error.NotFound("organization.user_not_found", "No user with that e-mail has signed in to Event Content Studio yet.");

    public static readonly Error LastOwner =
        Error.Conflict("organization.last_owner", "An organization must keep at least one owner.");

    public static readonly Error OwnerRoleRequired =
        Error.Forbidden("organization.owner_role_required", "Only owners can grant, change or remove the Owner role.");
}
