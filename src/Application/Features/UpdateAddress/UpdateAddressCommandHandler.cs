using System.Text.Json;
using Kart.Shared.Domain;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Models;
using Kart.User.Domain.Entities;
using Kart.User.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.Application.Features.UpdateAddress;

public sealed class UpdateAddressCommandHandler(IUserDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateAddressCommand, Result<AddressResponse>>
{
    public async Task<Result<AddressResponse>> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
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

        var result = profile.UpdateAddress(
            request.AddressId, type, request.Line1, request.Line2, request.City, request.Region,
            request.PostalCode, request.CountryCode, request.Phone, request.IsDefault,
            now, request.ActingPrincipalId);

        if (result.IsFailure)
        {
            return Result.Failure<AddressResponse>(result.Error);
        }

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            profile.UserId,
            eventType: "UserProfileUpdated",
            payloadJson: JsonSerializer.Serialize(new { userId = profile.UserId }),
            now,
            createdBy: request.ActingPrincipalId));

        await dbContext.SaveChangesAsync(cancellationToken);

        var address = result.Value;
        return Result.Success(new AddressResponse(
            address.AddressId, address.Type.ToString(), address.Line1, address.Line2,
            address.City, address.Region, address.PostalCode, address.CountryCode,
            address.Phone, address.IsDefault));
    }
}
