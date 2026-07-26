using Kart.Shared.Domain;
using Kart.User.Application.Common.Models;
using MediatR;

namespace Kart.User.Application.Features.GetUserProfile;

/// <summary>USR-2. <c>GET /v1/users/{userId}</c> — read from the MongoDB projection.</summary>
public sealed record GetUserProfileQuery(string UserId) : IRequest<Result<UserProfileResponse>>;
