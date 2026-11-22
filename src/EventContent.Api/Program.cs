using System.Security.Claims;
using System.Threading.RateLimiting;
using EventContent.Api.Auth;
using EventContent.Api.Endpoints;
using EventContent.Api.Http;
using EventContent.Application;
using EventContent.Application.Abstractions;
using EventContent.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging & telemetry -------------------------------------------------------------------
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
}
else
{
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
}

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("event-content-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal))
        .AddHttpClientInstrumentation()
        .AddSource("Npgsql"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    // Endpoint, headers and protocol come from the standard OTEL_* environment variables.
    otel.UseOtlpExporter();
}

// ---- Services --------------------------------------------------------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserAccessor>());
builder.Services.AddScoped<TenantContextAccessor>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContextAccessor>());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitPerMinute = builder.Configuration.GetValue("RateLimiting:RequestsPerMinute", 300);
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = 1 * 1024 * 1024; // JSON API; media uploads go straight to object storage.
});

// ---- Pipeline --------------------------------------------------------------------------------
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<UserProvisioningMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

app.MapOrganizationEndpoints();

await app.RunAsync();

/// <summary>Entry point marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
