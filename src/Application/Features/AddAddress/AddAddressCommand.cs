using Kart.Shared.Domain;
using Kart.User.Application.Common.Models;
using MediatR;

namespace Kart.User.Application.Features.AddAddress;

/// <summary>USR-4. <c>POST /v1/users/{userId}/addresses</c>.</summary>
public sealed record AddAddressCommand(
    string UserId,
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
