using Kart.User.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.User.Infrastructure.Persistence;

/// <summary>
/// Hand-written enum &lt;-&gt; lowercase-string converters, matching database-design.md's CHECK
/// constraints exactly (`'Shipping'/'Billing'/'Other'`, `'Active'/'Erased'`) rather than EF's
/// default enum-name conversion (which would emit the C# member name's exact casing and break if
/// the enum is ever reordered, since EF's implicit int conversion is ordinal-based).
/// </summary>
public static class EnumDbValueConverters
{
    public static readonly ValueConverter<AddressType, string> AddressTypeConverter = new(
        toDb => toDb.ToString(),
        fromDb => Enum.Parse<AddressType>(fromDb));

    public static readonly ValueConverter<ErasureStatus, string> ErasureStatusConverter = new(
        toDb => toDb.ToString(),
        fromDb => Enum.Parse<ErasureStatus>(fromDb));
}
