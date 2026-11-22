using System.Text.RegularExpressions;
using EventContent.Domain.Common;

namespace EventContent.Domain.Organizations;

/// <summary>
/// A tenant. Membership rules (owner protection, role escalation) are enforced here so they hold
/// regardless of which endpoint or job changes membership.
/// </summary>
public sealed partial class Organization
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 120;
    public const int SlugMinLength = 3;
    public const int SlugMaxLength = 63;

    private readonly List<OrganizationMember> _members = [];

    private Organization()
    {
        Name = string.Empty;
        Slug = string.Empty;
        DefaultTimeZone = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    /// <summary>IANA time zone identifier, e.g. <c>Africa/Lagos</c>.</summary>
    public string DefaultTimeZone { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public uint Version { get; private set; }

    /// <summary>
    /// Incremented on every membership change so that concurrent membership edits conflict on the
    /// organization row (protects invariants such as "at least one owner" against races).
    /// </summary>
    public int MembershipRevision { get; private set; }

    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    public static Result<Organization> Create(string name, string slug, string timeZone, Guid creatorUserId, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedSlug = slug?.Trim().ToLowerInvariant() ?? string.Empty;

        if (ValidateName(normalizedName) is { } nameError)
        {
            errors["name"] = [nameError];
        }

        if (ValidateSlug(normalizedSlug) is { } slugError)
        {
            errors["slug"] = [slugError];
        }

        if (!IsValidTimeZone(timeZone))
        {
            errors["defaultTimeZone"] = ["Use an IANA time zone such as Africa/Lagos."];
        }

        if (errors.Count > 0)
        {
            return Error.Validation("organization.invalid", "The organization details are invalid.", errors);
        }

        var organization = new Organization
        {
            Id = Guid.CreateVersion7(now),
            Name = normalizedName,
            Slug = normalizedSlug,
            DefaultTimeZone = timeZone.Trim(),
            CreatedBy = creatorUserId,
            CreatedAt = now,
        };
        organization._members.Add(OrganizationMember.Create(organization.Id, creatorUserId, OrganizationRole.Owner, now));
        return organization;
    }

    public Result Update(string name, string timeZone)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = name?.Trim() ?? string.Empty;
        if (ValidateName(normalizedName) is { } nameError)
        {
            errors["name"] = [nameError];
        }

        if (!IsValidTimeZone(timeZone))
        {
            errors["defaultTimeZone"] = ["Use an IANA time zone such as Africa/Lagos."];
        }

        if (errors.Count > 0)
        {
            return Error.Validation("organization.invalid", "The organization details are invalid.", errors);
        }

        Name = normalizedName;
        DefaultTimeZone = timeZone.Trim();
        return Result.Success();
    }

    public Result<OrganizationMember> AddMember(Guid userId, OrganizationRole role, OrganizationRole actorRole, DateTimeOffset now)
    {
        if (_members.Any(m => m.UserId == userId))
        {
            return OrganizationErrors.AlreadyMember;
        }

        if (role == OrganizationRole.Owner && actorRole != OrganizationRole.Owner)
        {
            return OrganizationErrors.OwnerRoleRequired;
        }

        var member = OrganizationMember.Create(Id, userId, role, now);
        _members.Add(member);
        MembershipRevision++;
        return member;
    }

    public Result ChangeMemberRole(Guid memberId, OrganizationRole newRole, OrganizationRole actorRole)
    {
        var member = _members.SingleOrDefault(m => m.Id == memberId);
        if (member is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        if (member.Role == newRole)
        {
            return Result.Success();
        }

        var touchesOwnerRole = member.Role == OrganizationRole.Owner || newRole == OrganizationRole.Owner;
        if (touchesOwnerRole && actorRole != OrganizationRole.Owner)
        {
            return OrganizationErrors.OwnerRoleRequired;
        }

        if (member.Role == OrganizationRole.Owner && OwnerCount() == 1)
        {
            return OrganizationErrors.LastOwner;
        }

        member.ChangeRole(newRole);
        MembershipRevision++;
        return Result.Success();
    }

    public Result<OrganizationMember> RemoveMember(Guid memberId, OrganizationRole actorRole)
    {
        var member = _members.SingleOrDefault(m => m.Id == memberId);
        if (member is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        if (member.Role == OrganizationRole.Owner)
        {
            if (actorRole != OrganizationRole.Owner)
            {
                return OrganizationErrors.OwnerRoleRequired;
            }

            if (OwnerCount() == 1)
            {
                return OrganizationErrors.LastOwner;
            }
        }

        _members.Remove(member);
        MembershipRevision++;
        return member;
    }

    public static bool IsValidTimeZone(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone.Trim(), out var zone)
        && zone.HasIanaId;

    private int OwnerCount() => _members.Count(m => m.Role == OrganizationRole.Owner);

    private static string? ValidateName(string name) =>
        name.Length is < NameMinLength or > NameMaxLength
            ? $"Name must be between {NameMinLength} and {NameMaxLength} characters."
            : null;

    private static string? ValidateSlug(string slug)
    {
        if (slug.Length is < SlugMinLength or > SlugMaxLength)
        {
            return $"URL name must be between {SlugMinLength} and {SlugMaxLength} characters.";
        }

        return SlugPattern().IsMatch(slug)
            ? null
            : "URL name may contain lowercase letters, digits and single hyphens, and cannot start or end with a hyphen.";
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SlugPattern();
}
