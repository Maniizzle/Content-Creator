using EventContent.Api.Http;
using EventContent.Application.Audit;
using EventContent.Application.Organizations;
using EventContent.Application.Users;
using EventContent.Contracts.Audit;
using EventContent.Contracts.Common;
using EventContent.Contracts.Organizations;
using EventContent.Contracts.Users;
using EventContent.Domain.Organizations;

namespace EventContent.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Organizations");

        api.MapGet("/me", async (GetCurrentUserHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("GetCurrentUser")
            .Produces<CurrentUserResponse>();

        api.MapPost("/organizations", async (
                CreateOrganizationRequest request, CreateOrganizationHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttp(http, org =>
                    TypedResults.Created($"/api/v1/organizations/{org.Id}", org)))
            .WithName("CreateOrganization")
            .Produces<OrganizationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        var org = api.MapGroup($"/organizations/{{{TenantEndpointFilter.OrganizationRouteKey}:guid}}").RequireTenant();

        org.MapGet(string.Empty, async (GetOrganizationHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(ct)).ToHttp(http, r => TypedResults.Ok(r)))
            .RequirePermission(Permission.ContentRead)
            .WithName("GetOrganization")
            .Produces<OrganizationResponse>();

        org.MapPatch(string.Empty, async (
                UpdateOrganizationRequest request, UpdateOrganizationHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttp(http, r => TypedResults.Ok(r)))
            .RequirePermission(Permission.OrganizationManage)
            .WithName("UpdateOrganization")
            .Produces<OrganizationResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        org.MapGet("/members", async (ListMembersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .RequirePermission(Permission.ContentRead)
            .WithName("ListMembers")
            .Produces<IReadOnlyList<MemberResponse>>();

        org.MapPost("/members", async (
                AddMemberRequest request, AddMemberHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttp(http, member => TypedResults.Created(
                    $"{http.Request.Path}/{member.Id}", member)))
            .RequirePermission(Permission.MembersManage)
            .WithName("AddMember")
            .Produces<MemberResponse>(StatusCodes.Status201Created);

        org.MapPatch("/members/{memberId:guid}", async (
                Guid memberId, ChangeMemberRoleRequest request, ChangeMemberRoleHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(memberId, request, ct)).ToHttp(http))
            .RequirePermission(Permission.MembersManage)
            .WithName("ChangeMemberRole");

        org.MapDelete("/members/{memberId:guid}", async (
                Guid memberId, RemoveMemberHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(memberId, ct)).ToHttp(http))
            .RequirePermission(Permission.MembersManage)
            .WithName("RemoveMember");

        org.MapGet("/audit-log", async (
                string? cursor, int? pageSize, ListAuditLogHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(cursor, pageSize, ct)).ToHttp(http, r => TypedResults.Ok(r)))
            .RequirePermission(Permission.OrganizationManage)
            .WithName("ListAuditLog")
            .Produces<PagedResponse<AuditLogEntryResponse>>();

        return app;
    }
}
