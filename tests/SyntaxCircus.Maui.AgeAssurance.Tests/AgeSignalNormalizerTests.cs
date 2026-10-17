namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Mirrors SyntaxCircus.Godot.AgeAssurance tests/test_age_assurance.gd (normalizer cases) plus the
// bound/mandatory rules of addons/syntaxcircus_age_assurance/age_signal.gd.
public class AgeSignalNormalizerTests
{
    private static NativeAgeSignal Shared(int? lower, int? upper = null) =>
        new() { Status = AgeStatus.Shared, Lower = lower, UpperExclusive = upper };

    [Fact]
    public void Thirteen_meets_thirteen() =>
        AgeSignalNormalizer.Normalize(Shared(13, 16), 13).Eligibility.ShouldBe(AgeEligibility.Eligible);

    [Fact]
    public void Exclusive_upper_thirteen_is_under_thirteen() =>
        AgeSignalNormalizer.Normalize(Shared(0, 13), 13).Eligibility.ShouldBe(AgeEligibility.BelowMinimum);

    [Fact]
    public void Overlapping_band_is_unknown() =>
        AgeSignalNormalizer.Normalize(Shared(13, 16), 15).Eligibility.ShouldBe(AgeEligibility.Unknown);

    [Fact]
    public void Band_including_minimum_is_not_wholly_below_minimum() =>
        AgeSignalNormalizer.Normalize(Shared(13, 15 + 1), 15).Restriction.ShouldBe(AgeRestriction.None);

    [Fact]
    public void Open_upper_band_is_eligible() =>
        AgeSignalNormalizer.Normalize(Shared(18), 18).Eligibility.ShouldBe(AgeEligibility.Eligible);

    [Fact]
    public void Missing_bounds_are_not_adult() =>
        AgeSignalNormalizer.Normalize(Shared(null), 13).Eligibility.ShouldBe(AgeEligibility.Unknown);

    [Fact]
    public void Missing_lower_with_upper_at_or_below_minimum_is_below_minimum()
    {
        var result = AgeSignalNormalizer.Normalize(Shared(null, 13), 13);
        result.Eligibility.ShouldBe(AgeEligibility.BelowMinimum);
        result.Restriction.ShouldBe(AgeRestriction.BelowMinimum);
    }

    [Fact]
    public void Missing_lower_with_upper_above_minimum_is_unknown() =>
        AgeSignalNormalizer.Normalize(Shared(null, 16), 13).Eligibility.ShouldBe(AgeEligibility.Unknown);

    public static TheoryData<NativeAgeSignal> UnknownOrMalformed => new()
    {
        new NativeAgeSignal { Status = AgeStatus.Declined },
        new NativeAgeSignal { Status = AgeStatus.Unavailable },
        Shared(-2),
        Shared(16, 13),
        Shared(13, 13),
        Shared(122),
        Shared(13, 122),
        Shared(null, 122),
        Shared(NativeAgeSignal.InvalidBound),
    };

    [Theory]
    [MemberData(nameof(UnknownOrMalformed))]
    public void Unknown_or_malformed_values_cannot_authorize(NativeAgeSignal raw) =>
        AgeSignalNormalizer.Normalize(raw, 13).Eligibility.ShouldBe(AgeEligibility.Unknown);

    [Fact]
    public void Bounds_at_edges_of_range_are_accepted()
    {
        AgeSignalNormalizer.Normalize(Shared(0, 121), 13).Eligibility.ShouldBe(AgeEligibility.Unknown);
        AgeSignalNormalizer.Normalize(Shared(121), 13).Eligibility.ShouldBe(AgeEligibility.Eligible);
    }

    [Fact]
    public void Non_positive_minimum_age_gives_no_verdict()
    {
        AgeSignalNormalizer.Normalize(Shared(18), 0).Eligibility.ShouldBe(AgeEligibility.Unknown);
        AgeSignalNormalizer.Normalize(Shared(18), -1).Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Theory]
    [InlineData(AgeApproval.Pending)]
    [InlineData(AgeApproval.Declined)]
    public void Approval_outranks_eligible_age(AgeApproval approval)
    {
        var raw = Shared(18) with { Approval = approval };
        var result = AgeSignalNormalizer.Normalize(raw, 13);
        result.Restriction.ShouldBe(AgeRestriction.ApprovalRequired);
        result.Approval.ShouldBe(approval);
    }

    [Fact]
    public void Approved_adds_no_restriction()
    {
        var result = AgeSignalNormalizer.Normalize(Shared(18) with { Approval = AgeApproval.Approved }, 13);
        result.Restriction.ShouldBe(AgeRestriction.None);
        result.Approval.ShouldBe(AgeApproval.Approved);
    }

    [Fact]
    public void Approval_outranks_below_minimum()
    {
        var result = AgeSignalNormalizer.Normalize(Shared(0, 13) with { Approval = AgeApproval.Pending }, 13);
        result.Eligibility.ShouldBe(AgeEligibility.BelowMinimum);
        result.Restriction.ShouldBe(AgeRestriction.ApprovalRequired);
    }

    [Fact]
    public void Mandatory_verification_is_distinct() =>
        AgeSignalNormalizer.Normalize(new NativeAgeSignal { Status = AgeStatus.VerificationRequired }, 13)
            .Restriction.ShouldBe(AgeRestriction.VerificationRequired);

    [Fact]
    public void Recheck_status_maps_to_recheck_restriction() =>
        AgeSignalNormalizer.Normalize(new NativeAgeSignal { Status = AgeStatus.RecheckRequired }, 13)
            .Restriction.ShouldBe(AgeRestriction.RecheckRequired);

    [Fact]
    public void Mandatory_sharing_has_no_fallback() =>
        AgeSignalNormalizer.Normalize(new NativeAgeSignal { Status = AgeStatus.Declined, Mandatory = true }, 13)
            .Restriction.ShouldBe(AgeRestriction.VerificationRequired);

    [Fact]
    public void Mandatory_with_known_eligibility_adds_no_restriction() =>
        AgeSignalNormalizer.Normalize(Shared(18) with { Mandatory = true }, 13)
            .Restriction.ShouldBe(AgeRestriction.None);

    [Fact]
    public void Mandatory_does_not_override_recheck() =>
        AgeSignalNormalizer.Normalize(new NativeAgeSignal { Status = AgeStatus.RecheckRequired, Mandatory = true }, 13)
            .Restriction.ShouldBe(AgeRestriction.RecheckRequired);

    [Fact]
    public void Regulatory_obligation_alone_is_not_pending_approval()
    {
        var raw = Shared(13) with { Requirements = AgeRequirements.ParentalConsent };
        var result = AgeSignalNormalizer.Normalize(raw, 13);
        result.Restriction.ShouldBe(AgeRestriction.None);
        result.Requirements.ShouldBe(AgeRequirements.ParentalConsent);
    }

    [Fact]
    public void Requirements_flags_are_carried()
    {
        var raw = Shared(13) with { Requirements = AgeRequirements.ParentalConsent | AgeRequirements.AdultNotification };
        AgeSignalNormalizer.Normalize(raw, 13).Requirements
            .ShouldBe(AgeRequirements.ParentalConsent | AgeRequirements.AdultNotification);
    }

    [Fact]
    public void Null_signal_is_unavailable() =>
        AgeSignalNormalizer.Normalize(null, 13).ShouldBe(AgeCheckResult.Unavailable);

    [Fact]
    public void Default_signal_is_unavailable_unknown_unrestricted()
    {
        var result = AgeSignalNormalizer.Normalize(new NativeAgeSignal(), 13);
        result.Status.ShouldBe(AgeStatus.Unavailable);
        result.Eligibility.ShouldBe(AgeEligibility.Unknown);
        result.Restriction.ShouldBe(AgeRestriction.None);
        result.Approval.ShouldBe(AgeApproval.Unknown);
        result.Requirements.ShouldBe(AgeRequirements.None);
    }

    [Fact]
    public void Result_exposes_no_age_bounds() =>
        typeof(AgeCheckResult).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(p => p.Name).ShouldBe(
            ["Status", "Eligibility", "Restriction", "Approval", "Requirements"], ignoreOrder: true);
}
