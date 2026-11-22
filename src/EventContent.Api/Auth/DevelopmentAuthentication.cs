using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EventContent.Api.Auth;

/// <summary>
/// Development-only authentication: every request is signed in as a configured local user, optionally
/// switched with the <c>X-Dev-User</c> header so multi-user/RBAC flows can be exercised locally.
/// It is refused at startup outside the Development environment (see <see cref="AuthSetup"/>).
/// Replace with OIDC (ADR 0002) before any shared deployment.
/// </summary>
public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<DevelopmentAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevelopmentAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";
    public const string Issuer = "urn:event-content:development";
    public const string UserHeader = "X-Dev-User";
    private const int MaxEmailLength = 254;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var requested = Request.Headers[UserHeader].ToString();
        var email = string.IsNullOrWhiteSpace(requested) ? Options.DefaultUserEmail : requested.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (email.Length > MaxEmailLength || email.Count(c => c == '@') != 1 || email.Any(char.IsWhiteSpace))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid development user e-mail."));
        }

        var normalized = email.ToLowerInvariant();
        var displayName = normalized[..normalized.IndexOf('@', StringComparison.Ordinal)];
        var identity = new ClaimsIdentity(
            [
                new Claim("iss", Issuer),
                new Claim("sub", normalized),
                new Claim(ClaimTypes.Email, normalized),
                new Claim(ClaimTypes.Name, displayName),
            ],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class DevelopmentAuthenticationOptions : AuthenticationSchemeOptions
{
    public string DefaultUserEmail { get; set; } = string.Empty;
}

public static class AuthSetup
{
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var mode = configuration["Auth:Mode"];
        switch (mode)
        {
            case "Development":
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Auth:Mode 'Development' is only allowed when ASPNETCORE_ENVIRONMENT=Development.");
                }

                services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                    .AddScheme<DevelopmentAuthenticationOptions, DevelopmentAuthenticationHandler>(
                        DevelopmentAuthenticationHandler.SchemeName,
                        o => o.DefaultUserEmail = configuration["Auth:Development:DefaultUserEmail"] ?? string.Empty);
                break;

            default:
                throw new InvalidOperationException(
                    $"Auth:Mode '{mode}' is not supported yet. Use 'Development' locally; OIDC is planned (ADR 0002).");
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        return services;
    }
}
