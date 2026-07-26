using Kart.Shared.Domain;
using Kart.User.Application.Common.Models;
using MediatR;

namespace Kart.User.Application.Features.UpdateUserPreferences;

public sealed record NotificationOptInInput(bool Email, bool Sms, bool Push);

public sealed record PreferencesInput(
    string? Locale,
    string? Currency,
    NotificationOptInInput? NotificationOptIn,
    bool MarketingConsent);

/// <summary>
/// USR-3. <c>PATCH /v1/users/{userId}</c> — whole-preferences-object replace (edge-cases.md
/// last-write-wins), not a field-level merge. <see cref="ActingPrincipalId"/> is the resolved
/// caller (self-scope ownership already enforced at the endpoint per requirement-spec.md §24.1.2)
/// and becomes the audit <c>updated_by</c> stamp.
/// </summary>
public sealed record UpdateUserPreferencesCommand(
    string UserId,
    string ActingPrincipalId,
    PreferencesInput? Preferences,
    bool? AppInstalled) : IRequest<Result<UserProfileResponse>>;
