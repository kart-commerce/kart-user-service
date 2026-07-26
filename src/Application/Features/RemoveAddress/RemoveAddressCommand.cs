using Kart.Shared.Domain;
using MediatR;

namespace Kart.User.Application.Features.RemoveAddress;

/// <summary>USR-6. <c>DELETE /v1/users/{userId}/addresses/{addressId}</c>.</summary>
public sealed record RemoveAddressCommand(string UserId, Guid AddressId, string ActingPrincipalId) : IRequest<Result>;
