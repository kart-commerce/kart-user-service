namespace Kart.User.Domain.Enums;

/// <summary>
/// ddd-model.md's <c>AddressType</c> value object. Persisted as lowercase text
/// (`Shipping`/`Billing`/`Other` in the api-contract.yaml enum, `shipping`/`billing`/`other`
/// in database-design.md's CHECK constraint) via <see cref="Kart.User.Infrastructure.Persistence.EnumDbValueConverters"/>.
/// </summary>
public enum AddressType
{
    Shipping,
    Billing,
    Other
}
