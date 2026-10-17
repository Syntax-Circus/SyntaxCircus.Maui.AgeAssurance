namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Pure mapping of Google Play Age Signals values to <see cref="NativeAgeSignal"/>. Port of the status mappings in
/// the Godot Android plugin (<c>SyntaxCircusAgeAssurance.kt</c>). Integer constants mirror
/// <c>AgeSignalsStatus</c>/<c>SignificantChangeStatus</c> and are checked against the bound Java constants at
/// compile time in the Android provider. Only bounds and status are read; install ids and the age range
/// source are never touched.
/// </summary>
internal static class PlayAgeSignalMapper
{
    internal const int AccessShared = 1;
    internal const int AccessNotShared = 2;
    internal const int AccessVerificationRequired = 3;
    internal const int ChangeApproved = 1;
    internal const int ChangePending = 2;
    internal const int ChangeDeclined = 3;

    /// <summary>Result of <c>requestAgeSignalsAccess</c> when not shared: verification required, otherwise declined.</summary>
    internal static NativeAgeSignal ForAccessDenied(int? accessStatus) => new()
    {
        Status = accessStatus == AccessVerificationRequired ? AgeStatus.VerificationRequired : AgeStatus.Declined,
    };

    /// <summary>Result of <c>checkAgeSignals</c>. A silent check that no longer yields any bound means previously shared access was lost.</summary>
    internal static NativeAgeSignal ForResult(int? ageLower, int? ageUpper, int? significantChangeStatus, bool allowPrompt)
    {
        // Silent range queries do not expose sharing/verification status. If a formerly shared range disappears,
        // require an explicit access recheck rather than assuming it is optional (or falsely claiming underage).
        var status = !allowPrompt && ageLower is null && ageUpper is null ? AgeStatus.RecheckRequired : AgeStatus.Shared;

        return new NativeAgeSignal
        {
            Status = status,
            Lower = ageLower,
            // The Play upper bound is inclusive; the common contract uses an exclusive bound.
            UpperExclusive = ageUpper + 1,
            Approval = significantChangeStatus switch
            {
                ChangePending => AgeApproval.Pending,
                ChangeDeclined => AgeApproval.Declined,
                ChangeApproved => AgeApproval.Approved,
                _ => AgeApproval.Unknown,
            },
        };
    }
}
