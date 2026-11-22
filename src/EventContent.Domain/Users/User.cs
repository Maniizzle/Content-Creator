namespace EventContent.Domain.Users;

/// <summary>
/// An application user. Identity is owned by an external identity provider and is identified by the
/// (<see cref="Issuer"/>, <see cref="Subject"/>) pair, never by e-mail.
/// </summary>
public sealed class User
{
    private User()
    {
        Issuer = string.Empty;
        Subject = string.Empty;
        Email = string.Empty;
        DisplayName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Issuer { get; private set; }

    public string Subject { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public static User Provision(string issuer, string subject, string email, string displayName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new User
        {
            Id = Guid.CreateVersion7(now),
            Issuer = issuer,
            Subject = subject,
            Email = NormalizeEmail(email),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Trim() : displayName.Trim(),
            CreatedAt = now,
            LastSeenAt = now,
        };
    }

    /// <summary>Refreshes profile claims from the identity provider on sign-in.</summary>
    public void RecordSignIn(string email, string displayName, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = NormalizeEmail(email);
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        LastSeenAt = now;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
