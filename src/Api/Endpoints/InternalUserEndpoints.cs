using Kart.Shared.ErrorHandling;
using Kart.User.Application.Features.ProcessErasureRequest;
using MediatR;

namespace Kart.User.Api.Endpoints;

/// <summary>
/// <c>POST /internal/v1/users/{userId}/erasure-requests</c> — ADR-0016/ADR-0017. Internal-only
/// (never reachable via the public API Gateway route table); gated on the caller carrying the
/// <c>admin</c> scope, the same distinguishing check kart-identity-service's own
/// <c>InternalUserEndpoints</c> uses for its client-credentials-only routes (a scope claim, not a
/// role claim — this distinguishes Admin Service's own service principal from an interactive
/// user token that happens to carry admin-level roles).
/// </summary>
public static class InternalUserEndpoints
{
    public static IEndpointRouteBuilder MapInternalUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/v1/users/{userId}/erasure-requests", SubmitErasureRequest)
            .WithName("submitErasureRequest")
            .RequireAuthorization(policy => policy.RequireClaim("scope", "admin"))
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SubmitErasureRequest(string userId, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        var actingPrincipalId = httpContext.User.FindFirst("client_id")?.Value ?? httpContext.User.FindFirst("sub")?.Value ?? "unknown";
        var result = await sender.Send(new ProcessErasureRequestCommand(userId, actingPrincipalId), cancellationToken);

        if (result.IsFailure)
        {
            var problem = KartProblemDetailsFactory.Create(httpContext, StatusCodes.Status404NotFound, result.Error.Code, result.Error.Message);
            return Results.Json(problem, statusCode: StatusCodes.Status404NotFound, contentType: "application/problem+json");
        }

        var response = new { userId = result.Value.UserId, acceptedAt = result.Value.AcceptedAt };

        // Idempotent no-op on repeat (ddd-model.md — erasure is one-directional): a second call
        // against an already-Erased profile still returns success, just 200 instead of 202.
        return result.Value.WasAlreadyErased ? Results.Ok(response) : Results.Accepted(value: response);
    }
}
