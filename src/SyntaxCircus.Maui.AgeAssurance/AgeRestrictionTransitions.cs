namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// State machine for the persisted <see cref="AgeRestriction"/> category. Ordinary failures cannot erase
/// known restrictions; age eligibility alone cannot resolve a separately pending approval.
/// </summary>
public static class AgeRestrictionTransitions
{
    /// <summary>Returns the restriction to persist after <paramref name="result"/>, given <paramref name="previous"/>.</summary>
    public static AgeRestriction Next(AgeRestriction previous, AgeCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (previous == AgeRestriction.ApprovalRequired && result.Approval != AgeApproval.Approved)
        {
            return previous;
        }

        var restriction = result.Restriction;
        if (restriction == AgeRestriction.RecheckRequired && previous != AgeRestriction.None)
        {
            return previous;
        }

        if (restriction != AgeRestriction.None)
        {
            return restriction;
        }

        if (previous == AgeRestriction.RecheckRequired && result.Status == AgeStatus.Declined)
        {
            return AgeRestriction.None;
        }

        if (result.Eligibility == AgeEligibility.Eligible
            && (previous != AgeRestriction.ApprovalRequired || result.Approval == AgeApproval.Approved))
        {
            return AgeRestriction.None;
        }

        return previous;
    }
}
