using Kart.Shared.Domain;
using Kart.User.Application.Common.Models;
using MediatR;

namespace Kart.User.Application.Features.UpdateAddress;

/// <summary>USR-5. <c>PATCH /v1/users/{userId}/addresses/{addressId}</c> — whole-record replace.</summary>
public sealed record UpdateAddressCommand(
    string UserId,
    Guid AddressId,
    string ActingPrincipalId,
    string Type,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    string? Phone,
    bool IsDefault) : IRequest<Result<AddressResponse>>;
