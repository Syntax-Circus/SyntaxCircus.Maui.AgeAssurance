namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>Outcome of the platform age-signal request itself (not of the user's age).</summary>
public enum AgeStatus
{
    /// <summary>No signal could be obtained (unsupported platform, not Play-managed, failure, timeout, concurrent call).</summary>
    Unavailable,

    /// <summary>The user shared an age range. Bounds are never exposed; see <see cref="AgeEligibility"/>.</summary>
    Shared,

    /// <summary>The user declined to share.</summary>
    Declined,

    /// <summary>The platform requires verification before an age signal is available.</summary>
    VerificationRequired,

    /// <summary>Previously shared access was lost; the app must ask again with prompting allowed.</summary>
    RecheckRequired,
}

/// <summary>Whether the shared range proves the user meets the caller's minimum age.</summary>
public enum AgeEligibility
{
    /// <summary>Cannot be determined. Never treat as adult.</summary>
    Unknown,

    /// <summary>The whole range is at or above the minimum age.</summary>
    Eligible,

    /// <summary>The whole range is below the minimum age.</summary>
    BelowMinimum,
}

/// <summary>Persistent restriction category. Consumers persist only this, never a platform range.</summary>
public enum AgeRestriction
{
    /// <summary>No restriction.</summary>
    None,

    /// <summary>The user is below the minimum age.</summary>
    BelowMinimum,

    /// <summary>The platform mandates age verification.</summary>
    VerificationRequired,

    /// <summary>A parental/guardian approval is pending or was declined.</summary>
    ApprovalRequired,

    /// <summary>Access must be re-requested explicitly.</summary>
    RecheckRequired,
}

/// <summary>Parental approval state, independent of age eligibility.</summary>
public enum AgeApproval
{
    /// <summary>No approval information.</summary>
    Unknown,

    /// <summary>Approved.</summary>
    Approved,

    /// <summary>Awaiting approval.</summary>
    Pending,

    /// <summary>Approval declined.</summary>
    Declined,
}

/// <summary>Regulatory obligations reported by the platform (obligations, not pending consent).</summary>
[Flags]
public enum AgeRequirements
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Significant-update parental consent is required.</summary>
    ParentalConsent = 1,

    /// <summary>Significant-update adult notification/acknowledgement is required.</summary>
    AdultNotification = 2,
}
