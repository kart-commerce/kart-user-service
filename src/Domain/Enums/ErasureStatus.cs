namespace Kart.User.Domain.Enums;

/// <summary>
/// ddd-model.md's <c>ErasureStatus</c> value object — a one-directional tombstone marker
/// (ADR-0016). Nothing in requirement-spec.md, edge-cases.md, or ADR-0016 defines or asks for
/// an "un-erase" path back to <see cref="Active"/>.
/// </summary>
public enum ErasureStatus
{
    Active,
    Erased
}
