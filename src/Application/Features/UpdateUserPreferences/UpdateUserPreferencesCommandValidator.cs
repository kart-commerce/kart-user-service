using FluentValidation;

namespace Kart.User.Application.Features.UpdateUserPreferences;

public sealed class UpdateUserPreferencesCommandValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesCommandValidator()
    {
        RuleFor(x => x.Preferences!.Currency)
            .Length(3)
            .When(x => x.Preferences?.Currency is not null)
            .WithMessage("currency must be a 3-letter ISO 4217 code.");
    }
}
