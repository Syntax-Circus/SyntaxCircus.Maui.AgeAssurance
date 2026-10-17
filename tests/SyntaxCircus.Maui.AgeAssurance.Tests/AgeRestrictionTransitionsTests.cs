namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Mirrors SyntaxCircus.Godot.AgeAssurance addons/syntaxcircus_age_assurance/age_restriction.gd (the Godot
// repo has no dedicated test file for it, so each branch of the rule is covered here, plus the README
// invariants: failures cannot clear a known restriction; a confirmed optional decline clears only recheck).
public class AgeRestrictionTransitionsTests
{
    private static AgeCheckResult R(
        AgeStatus status = AgeStatus.Unavailable,
        AgeEligibility eligibility = AgeEligibility.Unknown,
        AgeRestriction restriction = AgeRestriction.None,
        AgeApproval approval = AgeApproval.Unknown) =>
        new() { Status = status, Eligibility = eligibility, Restriction = restriction, Approval = approval };

    [Theory]
    [InlineData(AgeApproval.Unknown)]
    [InlineData(AgeApproval.Pending)]
    [InlineData(AgeApproval.Declined)]
    public void Approval_required_sticks_until_approved(AgeApproval approval)
    {
        AgeRestrictionTransitions.Next(AgeRestriction.ApprovalRequired, R(AgeStatus.Shared, AgeEligibility.Eligible, approval: approval))
            .ShouldBe(AgeRestriction.ApprovalRequired);
        AgeRestrictionTransitions.Next(AgeRestriction.ApprovalRequired, R(restriction: AgeRestriction.BelowMinimum, approval: approval))
            .ShouldBe(AgeRestriction.ApprovalRequired);
    }

    [Fact]
    public void Approval_required_clears_when_approved_and_eligible() =>
        AgeRestrictionTransitions.Next(AgeRestriction.ApprovalRequired, R(AgeStatus.Shared, AgeEligibility.Eligible, approval: AgeApproval.Approved))
            .ShouldBe(AgeRestriction.None);

    [Fact]
    public void Approval_required_stays_when_approved_but_eligibility_unknown() =>
        AgeRestrictionTransitions.Next(AgeRestriction.ApprovalRequired, R(AgeStatus.Unavailable, approval: AgeApproval.Approved))
            .ShouldBe(AgeRestriction.ApprovalRequired);

    [Fact]
    public void Approved_with_new_restriction_moves_to_it() =>
        AgeRestrictionTransitions.Next(AgeRestriction.ApprovalRequired,
            R(AgeStatus.Shared, AgeEligibility.BelowMinimum, AgeRestriction.BelowMinimum, AgeApproval.Approved))
            .ShouldBe(AgeRestriction.BelowMinimum);

    [Theory]
    [InlineData(AgeRestriction.BelowMinimum)]
    [InlineData(AgeRestriction.VerificationRequired)]
    [InlineData(AgeRestriction.ApprovalRequired)]
    public void Recheck_result_cannot_replace_a_known_restriction(AgeRestriction previous) =>
        AgeRestrictionTransitions.Next(previous, R(AgeStatus.RecheckRequired, restriction: AgeRestriction.RecheckRequired, approval: AgeApproval.Approved))
            .ShouldBe(previous);

    [Fact]
    public void Recheck_result_applies_when_no_previous_restriction() =>
        AgeRestrictionTransitions.Next(AgeRestriction.None, R(AgeStatus.RecheckRequired, restriction: AgeRestriction.RecheckRequired))
            .ShouldBe(AgeRestriction.RecheckRequired);

    [Theory]
    [InlineData(AgeRestriction.BelowMinimum)]
    [InlineData(AgeRestriction.VerificationRequired)]
    [InlineData(AgeRestriction.ApprovalRequired)]
    public void Concrete_restrictions_replace_previous(AgeRestriction restriction) =>
        AgeRestrictionTransitions.Next(AgeRestriction.None, R(restriction: restriction, approval: AgeApproval.Approved))
            .ShouldBe(restriction);

    [Fact]
    public void Verification_replaces_below_minimum() =>
        AgeRestrictionTransitions.Next(AgeRestriction.BelowMinimum, R(restriction: AgeRestriction.VerificationRequired))
            .ShouldBe(AgeRestriction.VerificationRequired);

    [Fact]
    public void Confirmed_optional_decline_clears_recheck() =>
        AgeRestrictionTransitions.Next(AgeRestriction.RecheckRequired, R(AgeStatus.Declined))
            .ShouldBe(AgeRestriction.None);

    [Theory]
    [InlineData(AgeRestriction.BelowMinimum)]
    [InlineData(AgeRestriction.VerificationRequired)]
    public void Decline_does_not_clear_other_restrictions(AgeRestriction previous) =>
        AgeRestrictionTransitions.Next(previous, R(AgeStatus.Declined)).ShouldBe(previous);

    [Theory]
    [InlineData(AgeRestriction.BelowMinimum)]
    [InlineData(AgeRestriction.VerificationRequired)]
    [InlineData(AgeRestriction.RecheckRequired)]
    public void Eligible_age_clears_non_approval_restrictions(AgeRestriction previous) =>
        AgeRestrictionTransitions.Next(previous, R(AgeStatus.Shared, AgeEligibility.Eligible))
            .ShouldBe(AgeRestriction.None);

    [Theory]
    [InlineData(AgeRestriction.None)]
    [InlineData(AgeRestriction.BelowMinimum)]
    [InlineData(AgeRestriction.VerificationRequired)]
    [InlineData(AgeRestriction.ApprovalRequired)]
    [InlineData(AgeRestriction.RecheckRequired)]
    public void Ordinary_failure_cannot_clear_a_known_restriction(AgeRestriction previous) =>
        AgeRestrictionTransitions.Next(previous, AgeCheckResult.Unavailable).ShouldBe(previous);

    [Fact]
    public void Null_result_throws() =>
        Should.Throw<ArgumentNullException>(() => AgeRestrictionTransitions.Next(AgeRestriction.None, null!));
}
