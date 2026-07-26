using Kart.Shared.Domain;
using MediatR;

namespace Kart.User.Application.Features.ProcessErasureRequest;

public sealed record ErasureResponse(string UserId, DateTimeOffset AcceptedAt, bool WasAlreadyErased);

/// <summary>
/// USR-8. <c>POST /internal/v1/users/{userId}/erasure-requests</c> — Admin-only (ADR-0017),
/// idempotent no-op on repeat (ddd-model.md — erasure is one-directional).
/// </summary>
public sealed record ProcessErasureRequestCommand(string UserId, string ActingPrincipalId) : IRequest<Result<ErasureResponse>>;
