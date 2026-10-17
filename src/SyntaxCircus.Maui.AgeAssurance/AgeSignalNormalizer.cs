namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Turns a raw <see cref="NativeAgeSignal"/> into an <see cref="AgeCheckResult"/>. Never infers adult
/// status from missing bounds or from sharing status alone.
/// </summary>
public static class AgeSignalNormalizer
{
    private const int MaxBound = 121;

    /// <summary>
    /// Normalizes <paramref name="raw"/> against <paramref name="minimumAge"/>. Bounds outside 0..121 are
    /// rejected. A non-positive <paramref name="minimumAge"/> never yields an eligibility verdict.
    /// </summary>
    public static AgeCheckResult Normalize(NativeAgeSignal? raw, int minimumAge)
    {
        raw ??= new NativeAgeSignal();
        var restriction = AgeRestriction.None;
        var eligibility = AgeEligibility.Unknown;

        if (raw.Approval is AgeApproval.Pending or AgeApproval.Declined)
        {
            restriction = AgeRestriction.ApprovalRequired;
        }
        else if (raw.Status == AgeStatus.VerificationRequired)
        {
            restriction = AgeRestriction.VerificationRequired;
        }
        else if (raw.Status == AgeStatus.RecheckRequired)
        {
            restriction = AgeRestriction.RecheckRequired;
        }

        if (raw.Status == AgeStatus.Shared && minimumAge > 0)
        {
            var lower = raw.Lower;
            var upper = raw.UpperExclusive;
            var validLower = IsBound(lower);
            var validUpper = IsBound(upper);

            if (validLower && (upper is null || (validUpper && upper > lower)))
            {
                if (lower >= minimumAge)
                {
                    eligibility = AgeEligibility.Eligible;
                }
                else if (validUpper && upper <= minimumAge)
                {
                    eligibility = AgeEligibility.BelowMinimum;
                }
            }
            else if (lower is null && validUpper && upper <= minimumAge)
            {
                eligibility = AgeEligibility.BelowMinimum;
            }
        }

        if (eligibility == AgeEligibility.BelowMinimum && restriction == AgeRestriction.None)
        {
            restriction = AgeRestriction.BelowMinimum;
        }

        if (raw.Mandatory && eligibility == AgeEligibility.Unknown && restriction == AgeRestriction.None)
        {
            restriction = AgeRestriction.VerificationRequired;
        }

        return new AgeCheckResult
        {
            Status = raw.Status,
            Eligibility = eligibility,
            Restriction = restriction,
            Approval = raw.Approval,
            Requirements = raw.Requirements,
        };
    }

    private static bool IsBound(int? value) => value is >= 0 and <= MaxBound;
}
