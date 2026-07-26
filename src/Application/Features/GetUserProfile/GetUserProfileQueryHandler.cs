using Kart.Shared.Domain;
using Kart.User.Application.Common.Interfaces;
using Kart.User.Application.Common.Models;
using MediatR;

namespace Kart.User.Application.Features.GetUserProfile;

public sealed class GetUserProfileQueryHandler(IUserReadModelRepository readModel)
    : IRequestHandler<GetUserProfileQuery, Result<UserProfileResponse>>
{
    public async Task<Result<UserProfileResponse>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await readModel.GetByIdAsync(request.UserId, cancellationToken);
        return profile is null
            ? Result.Failure<UserProfileResponse>(Error.NotFound($"No profile exists for user '{request.UserId}'."))
            : Result.Success(profile);
    }
}
