namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Mirrors the status mappings of the Godot Android plugin (SyntaxCircusAgeAssurance.kt).
public class PlayAgeSignalMapperTests
{
    [Fact]
    public void Upper_bound_is_inclusive_in_play_and_becomes_exclusive()
    {
        var signal = PlayAgeSignalMapper.ForResult(13, 15, null, allowPrompt: true);
        signal.Status.ShouldBe(AgeStatus.Shared);
        signal.Lower.ShouldBe(13);
        signal.UpperExclusive.ShouldBe(16);
    }

    [Fact]
    public void Missing_bounds_stay_unbounded()
    {
        var signal = PlayAgeSignalMapper.ForResult(18, null, null, allowPrompt: true);
        signal.UpperExclusive.ShouldBeNull();
        AgeSignalNormalizer.Normalize(signal, 16).Eligibility.ShouldBe(AgeEligibility.Eligible);
    }

    [Fact]
    public void Prompted_check_without_bounds_is_shared_but_unknown()
    {
        var result = AgeSignalNormalizer.Normalize(PlayAgeSignalMapper.ForResult(null, null, null, allowPrompt: true), 13);
        result.Status.ShouldBe(AgeStatus.Shared);
        result.Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Fact]
    public void Silent_check_that_lost_all_bounds_requires_recheck()
    {
        var result = AgeSignalNormalizer.Normalize(PlayAgeSignalMapper.ForResult(null, null, null, allowPrompt: false), 13);
        result.Status.ShouldBe(AgeStatus.RecheckRequired);
        result.Restriction.ShouldBe(AgeRestriction.RecheckRequired);
    }

    [Fact]
    public void Silent_check_with_a_bound_stays_shared()
    {
        PlayAgeSignalMapper.ForResult(null, 12, null, allowPrompt: false).Status.ShouldBe(AgeStatus.Shared);
        PlayAgeSignalMapper.ForResult(18, null, null, allowPrompt: false).Status.ShouldBe(AgeStatus.Shared);
    }

    [Theory]
    [InlineData(PlayAgeSignalMapper.ChangePending, AgeApproval.Pending)]
    [InlineData(PlayAgeSignalMapper.ChangeDeclined, AgeApproval.Declined)]
    [InlineData(PlayAgeSignalMapper.ChangeApproved, AgeApproval.Approved)]
    [InlineData(0, AgeApproval.Unknown)]
    [InlineData(99, AgeApproval.Unknown)]
    public void Significant_change_status_maps_to_approval(int change, AgeApproval expected) =>
        PlayAgeSignalMapper.ForResult(18, null, change, allowPrompt: true).Approval.ShouldBe(expected);

    [Fact]
    public void Missing_significant_change_status_is_unknown_approval() =>
        PlayAgeSignalMapper.ForResult(18, null, null, allowPrompt: true).Approval.ShouldBe(AgeApproval.Unknown);

    [Theory]
    [InlineData(PlayAgeSignalMapper.AccessVerificationRequired, AgeStatus.VerificationRequired)]
    [InlineData(PlayAgeSignalMapper.AccessNotShared, AgeStatus.Declined)]
    [InlineData(0, AgeStatus.Declined)]
    [InlineData(null, AgeStatus.Declined)]
    public void Non_shared_access_maps_to_verification_or_declined(int? access, AgeStatus expected) =>
        PlayAgeSignalMapper.ForAccessDenied(access).Status.ShouldBe(expected);
}
