using Kart.User.Application.Common.Interfaces;

namespace Kart.User.UnitTests.TestSupport;

public sealed class FixedDateTimeProvider(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;
}
