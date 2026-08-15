using Kart.Shared.ErrorHandling;
using Kart.Shared.Domain;
using Kart.Shared.Observability;
using Kart.User.Application.Common;
using Kart.User.Application.Common.Models;
using Kart.User.Application.Features.AddAddress;
using Kart.User.Application.Features.GetUserProfile;
using Kart.User.Application.Features.RemoveAddress;
using Kart.User.Application.Features.UpdateAddress;
using Kart.User.Application.Features.UpdateUserPreferences;
using MediatR;

namespace Kart.User.Api.Endpoints;

/// <summary>
/// api-contract.yaml's client-facing surface: <c>/v1/users/{userId}</c> and its address-book
/// sub-resource. Every endpoint here — including the profile GET, previously left
/// `AllowAnonymous()` despite returning the full address book (street lines, phone numbers) for
/// any guessed userId, closed as part of the User Registration, Login &amp; Authentication
/// Journey flow build — is <c>self</c>-scoped (requirement-spec.md §24.1.2: "a one-line
/// resource.userId == token.sub check") — enforced inline, not via a separate
/// authorization-policy abstraction, since it genuinely is a one-line comparison.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/users").RequireAuthorization();

        group.MapGet("/{userId}", GetUserProfile)
            .WithName("getUserProfile")
            .Produces<UserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{userId}", UpdateUserPreferences)
            .WithName("updateUserPreferences")
            .Produces<UserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{userId}/addresses", AddAddress)
            .WithName("addAddress")
            .Produces<AddressResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{userId}/addresses/{addressId:guid}", UpdateAddress)
            .WithName("updateAddress")
            .Produces<AddressResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{userId}/addresses/{addressId:guid}", RemoveAddress)
            .WithName("removeAddress")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetUserProfile(string userId, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        if (!IsSelf(httpContext, userId))
        {
            return Forbid(httpContext);
        }

        var result = await sender.Send(new GetUserProfileQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : NotFound(httpContext, result.Error);
    }

    private static async Task<IResult> UpdateUserPreferences(
        string userId, UpdatePreferencesRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        if (!IsSelf(httpContext, userId))
        {
            return Forbid(httpContext);
        }

        var command = new UpdateUserPreferencesCommand(
            userId,
            userId,
            request.Preferences is null ? null : new PreferencesInput(
                request.Preferences.Locale,
                request.Preferences.Currency,
                request.Preferences.NotificationOptIn is null ? null : new NotificationOptInInput(
                    request.Preferences.NotificationOptIn.Email,
                    request.Preferences.NotificationOptIn.Sms,
                    request.Preferences.NotificationOptIn.Push),
                request.Preferences.MarketingConsent),
            request.AppInstalled);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : NotFound(httpContext, result.Error);
    }

    private static async Task<IResult> AddAddress(
        string userId, AddressRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);

        if (!IsSelf(httpContext, userId))
        {
            return Forbid(httpContext);
        }

        var command = new AddAddressCommand(
            userId, userId, request.Type, request.Line1, request.Line2, request.City,
            request.Region, request.PostalCode, request.CountryCode, request.Phone, request.IsDefault);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/v1/users/{userId}/addresses/{result.Value.AddressId}", result.Value)
            : NotFound(httpContext, result.Error);
    }

    private static async Task<IResult> UpdateAddress(
        string userId, Guid addressId, AddressRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        if (!IsSelf(httpContext, userId))
        {
            return Forbid(httpContext);
        }

        var command = new UpdateAddressCommand(
            userId, addressId, userId, request.Type, request.Line1, request.Line2, request.City,
            request.Region, request.PostalCode, request.CountryCode, request.Phone, request.IsDefault);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : NotFound(httpContext, result.Error);
    }

    private static async Task<IResult> RemoveAddress(
        string userId, Guid addressId, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        if (!IsSelf(httpContext, userId))
        {
            return Forbid(httpContext);
        }

        var result = await sender.Send(new RemoveAddressCommand(userId, addressId, userId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : NotFound(httpContext, result.Error);
    }

    private static bool IsSelf(HttpContext httpContext, string userId) =>
        httpContext.User.FindFirst("sub")?.Value == userId;

    private static IResult Forbid(HttpContext httpContext) => AsResult(
        httpContext, StatusCodes.Status403Forbidden, "forbidden", "You may only act on your own profile.");

    private static IResult NotFound(HttpContext httpContext, Error error) =>
        AsResult(httpContext, StatusCodes.Status404NotFound, error.Code, error.Message);

    private static IResult AsResult(HttpContext httpContext, int statusCode, string errorCode, string detail)
    {
        var problem = KartProblemDetailsFactory.Create(httpContext, statusCode, errorCode, detail);
        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }

    private sealed record NotificationOptInRequest(bool Email, bool Sms, bool Push);

    private sealed record PreferencesRequest(string? Locale, string? Currency, NotificationOptInRequest? NotificationOptIn, bool MarketingConsent);

    private sealed record UpdatePreferencesRequest(PreferencesRequest? Preferences, bool? AppInstalled);

    private sealed record AddressRequest(
        string Type, string Line1, string? Line2, string City, string? Region,
        string PostalCode, string CountryCode, string? Phone, bool IsDefault = false);
}
