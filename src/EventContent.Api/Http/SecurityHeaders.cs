namespace EventContent.Api.Http;

/// <summary>Conservative headers for a JSON-only API that is never rendered in a browser frame.</summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                headers["Cross-Origin-Resource-Policy"] = "same-site";
                headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next(context);
        });
}
