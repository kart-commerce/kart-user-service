using System.Text.Json;
using Kart.Shared.Domain;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Models;
using Kart.User.Domain.Entities;
using Kart.User.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.User.Application.Features.AddAddress;

public sealed class AddAddressCommandHandler(
    IUserDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AddAddressCommandHandler> logger)
    : IRequestHandler<AddAddressCommand, Result<AddressResponse>>
{
    public async Task<Result<AddressResponse>> Handle(AddAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .Include(p => p.Addresses)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<AddressResponse>(Error.NotFound($"No profile exists for user '{request.UserId}'."));
        }

        var now = dateTimeProvider.UtcNow;
        var type = Enum.Parse<AddressType>(request.Type, ignoreCase: true);

        var address = profile.AddAddress(
            type, request.Line1, request.Line2, request.City, request.Region,
            request.PostalCode, request.CountryCode, request.Phone, request.IsDefault,
            now, request.ActingPrincipalId);

        // Explicit Add() — a new Address reachable only via graph-fixup from the already-tracked
        // parent's Include'd, backing-field-only Addresses collection is not reliably detected as
        // Added by EF's change tracker (observed as State=Modified against a nonexistent row);
        // registering it directly with the DbSet removes the ambiguity.
        dbContext.Addresses.Add(address);

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            profile.UserId,
            eventType: "UserProfileUpdated",
            payloadJson: JsonSerializer.Serialize(new { userId = profile.UserId }),
            now,
            createdBy: request.ActingPrincipalId));

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Stage {Stage}: address persisted for user {UserId}", "AddressPersisted", profile.UserId);

        return Result.Success(new AddressResponse(
            address.AddressId, address.Type.ToString(), address.Line1, address.Line2,
            address.City, address.Region, address.PostalCode, address.CountryCode,
            address.Phone, address.IsDefault));
    }
}
