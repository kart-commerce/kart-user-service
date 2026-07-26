using FluentValidation;
using Kart.User.Application.Common.Validation;
using Kart.User.Domain.Enums;

namespace Kart.User.Application.Features.AddAddress;

public sealed class AddAddressCommandValidator : AbstractValidator<AddAddressCommand>
{
    public AddAddressCommandValidator()
    {
        RuleFor(x => x.Type).Must(t => Enum.TryParse<AddressType>(t, ignoreCase: true, out _))
            .WithMessage("type must be one of Shipping, Billing, Other.");
        RuleFor(x => x.Line1).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.PostalCode).NotEmpty();
        RuleFor(x => x)
            .Must(x => PostalCodePatterns.IsValid(x.CountryCode, x.PostalCode))
            .WithName("postalCode")
            .WithMessage(x => $"'{x.PostalCode}' is not a valid postal code format for country '{x.CountryCode}'.")
            .When(x => !string.IsNullOrWhiteSpace(x.CountryCode) && !string.IsNullOrWhiteSpace(x.PostalCode));
    }
}
