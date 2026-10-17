namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Normalized, policy-free result of an age check. Deliberately carries no age bounds; those stay
/// in memory inside the library and must never be logged, stored or sent.
/// </summary>
public sealed record AgeCheckResult
{
    /// <summary>Outcome of the platform request.</summary>
    public AgeStatus Status { get; init; } = AgeStatus.Unavailable;

    /// <summary>Whether the shared range proves the minimum age.</summary>
    public AgeEligibility Eligibility { get; init; } = AgeEligibility.Unknown;

    /// <summary>Restriction category derived from the signal.</summary>
    public AgeRestriction Restriction { get; init; } = AgeRestriction.None;

    /// <summary>Approval state, independent of eligibility.</summary>
    public AgeApproval Approval { get; init; } = AgeApproval.Unknown;

    /// <summary>Regulatory obligations reported by the platform.</summary>
    public AgeRequirements Requirements { get; init; } = AgeRequirements.None;

    /// <summary>The result for "no signal": unavailable, unknown eligibility, no restriction.</summary>
    public static AgeCheckResult Unavailable { get; } = new();
}
