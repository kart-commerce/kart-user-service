namespace Kart.User.Application.Common.Interfaces;

/// <summary>Testability seam for "now" — handlers never call <see cref="DateTimeOffset.UtcNow"/> directly.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
