using System.Text.Json;
using Kart.Shared.Domain;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.User.Application.Features.RemoveAddress;

public sealed class RemoveAddressCommandHandler(IUserDbContext dbContext, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RemoveAddressCommand, Result>
{
    public async Task<Result> Handle(RemoveAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles
            .Include(p => p.Addresses)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(Error.NotFound($"No profile exists for user '{request.UserId}'."));
        }

        var now = dateTimeProvider.UtcNow;
        var addressToRemove = profile.Addresses.FirstOrDefault(a => a.AddressId == request.AddressId);

        var result = profile.RemoveAddress(request.AddressId, now, request.ActingPrincipalId);
        if (result.IsFailure)
        {
            return result;
        }

        // Explicit Remove() — same graph-fixup ambiguity AddAddressCommandHandler documents for
        // Add(): don't rely solely on the domain method's removal from the tracked parent's
        // backing-field collection to produce a DELETE.
        dbContext.Addresses.Remove(addressToRemove!);

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            profile.UserId,
            eventType: "UserProfileUpdated",
            payloadJson: JsonSerializer.Serialize(new { userId = profile.UserId }),
            now,
            createdBy: request.ActingPrincipalId));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
