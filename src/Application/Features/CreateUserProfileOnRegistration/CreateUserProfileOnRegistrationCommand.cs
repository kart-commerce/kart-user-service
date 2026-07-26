using MediatR;

namespace Kart.User.Application.Features.CreateUserProfileOnRegistration;

/// <summary>
/// USR-1. Consumed from Identity's <c>UserRegistered</c> (<c>userId</c>, <c>email</c>) —
/// aggregate-creation trigger only (ddd-model.md). Dispatched by
/// <c>Infrastructure/Messaging/UserRegisteredConsumerHostedService</c>.
/// </summary>
public sealed record CreateUserProfileOnRegistrationCommand(string UserId, string? Email) : IRequest;
