namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Mirrors SyntaxCircus.Godot.AgeAssurance tests/test_native_adapter.gd (session reuse / cancel cases) and the
// _last_wire logic of addons/syntaxcircus_age_assurance/age_provider.gd.
public class AgeSignalSessionTests
{
    private static NativeAgeSignal Shared18 => new() { Status = AgeStatus.Shared, Lower = 18 };
    private static NativeAgeSignal Reuse => new() { Status = AgeStatus.Unavailable, ReuseSession = true };

    [Fact]
    public void Silent_apple_result_reevaluates_only_the_last_completed_range()
    {
        var session = new AgeSignalSession();
        AgeSignalNormalizer.Normalize(session.Resolve(Shared18, allowPrompt: true), 13).Eligibility.ShouldBe(AgeEligibility.Eligible);

        var result = AgeSignalNormalizer.Normalize(session.Resolve(Reuse, allowPrompt: false), 16);

        result.Eligibility.ShouldBe(AgeEligibility.Eligible);
        result.Status.ShouldBe(AgeStatus.Shared);
    }

    [Fact]
    public void Reuse_with_no_session_is_unavailable_and_unknown()
    {
        var result = AgeSignalNormalizer.Normalize(new AgeSignalSession().Resolve(Reuse, false), 13);
        result.Status.ShouldBe(AgeStatus.Unavailable);
        result.Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Fact]
    public void Cancelled_requests_cannot_repopulate_the_session_cache()
    {
        var session = new AgeSignalSession();
        session.Resolve(Shared18, true);
        session.Clear();

        AgeSignalNormalizer.Normalize(session.Resolve(Reuse, false), 13).Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Fact]
    public void Reuse_takes_mandatory_and_requirements_from_the_new_signal()
    {
        var session = new AgeSignalSession();
        session.Resolve(Shared18 with { Mandatory = true, Requirements = AgeRequirements.AdultNotification }, true);

        var resolved = session.Resolve(Reuse with { Mandatory = false, Requirements = AgeRequirements.ParentalConsent }, false);

        resolved.Mandatory.ShouldBeFalse();
        resolved.Requirements.ShouldBe(AgeRequirements.ParentalConsent);
        resolved.Lower.ShouldBe(18);
        resolved.ReuseSession.ShouldBeFalse();
    }

    [Fact]
    public void Reuse_keeps_remembered_approval()
    {
        var session = new AgeSignalSession();
        session.Resolve(Shared18 with { Approval = AgeApproval.Pending }, true);

        AgeSignalNormalizer.Normalize(session.Resolve(Reuse, false), 13).Restriction.ShouldBe(AgeRestriction.ApprovalRequired);
    }

    [Fact]
    public void Silent_non_reuse_result_does_not_update_the_session()
    {
        var session = new AgeSignalSession();
        session.Resolve(Shared18, true);
        session.Resolve(new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 0, UpperExclusive = 13 }, allowPrompt: false);

        AgeSignalNormalizer.Normalize(session.Resolve(Reuse, false), 13).Eligibility.ShouldBe(AgeEligibility.Eligible);
    }

    [Fact]
    public void Prompted_result_replaces_the_session()
    {
        var session = new AgeSignalSession();
        session.Resolve(Shared18, true);
        session.Resolve(new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 0, UpperExclusive = 13 }, true);

        AgeSignalNormalizer.Normalize(session.Resolve(Reuse, false), 13).Eligibility.ShouldBe(AgeEligibility.BelowMinimum);
    }

    [Fact]
    public void Resolving_a_reuse_signal_does_not_alter_the_remembered_range()
    {
        var session = new AgeSignalSession();
        session.Resolve(Shared18, true);
        session.Resolve(Reuse with { Mandatory = true }, true);

        session.Resolve(Reuse, false).Mandatory.ShouldBeFalse();
    }

    [Fact]
    public void Null_signal_throws() =>
        Should.Throw<ArgumentNullException>(() => new AgeSignalSession().Resolve(null!, true));
}
