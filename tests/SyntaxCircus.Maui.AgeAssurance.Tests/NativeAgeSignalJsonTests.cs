namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Mirrors SyntaxCircus.Godot.AgeAssurance tests/test_native_adapter.gd (JSON wire parsing: native JSON is
// normalized, malformed JSON cannot authorize) and the string/type handling in age_signal.gd.
public class NativeAgeSignalJsonTests
{
    private static AgeCheckResult Check(string? json, int min = 13) =>
        AgeSignalNormalizer.Normalize(NativeAgeSignal.FromJson(json), min);

    [Fact]
    public void Native_json_is_normalized() =>
        Check("""{"status":"shared","lower":18}""").Eligibility.ShouldBe(AgeEligibility.Eligible);

    [Theory]
    [InlineData("""{"status":"shared","lower":18.0}""", AgeEligibility.Eligible)]
    [InlineData("""{"status":"shared","lower":18.5}""", AgeEligibility.Unknown)]
    [InlineData("""{"status":"shared","lower":"18"}""", AgeEligibility.Unknown)]
    [InlineData("""{"status":"shared","lower":-2}""", AgeEligibility.Unknown)]
    [InlineData("""{"status":"shared","lower":16,"upper_exclusive":13}""", AgeEligibility.Unknown)]
    [InlineData("""{"status":"shared","upper_exclusive":"13"}""", AgeEligibility.Unknown)]
    [InlineData("""{"status":"shared","upper_exclusive":13}""", AgeEligibility.BelowMinimum)]
    [InlineData("""{"status":"shared","lower":null}""", AgeEligibility.Unknown)]
    public void Bounds_must_be_whole_numbers_in_range(string json, AgeEligibility expected) =>
        Check(json).Eligibility.ShouldBe(expected);

    [Theory]
    [InlineData("malformed")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("[1,2]")]
    [InlineData("\"shared\"")]
    [InlineData("{\"status\":")]
    public void Malformed_native_json_cannot_authorize(string? json)
    {
        var result = Check(json);
        result.Status.ShouldBe(AgeStatus.Unavailable);
        result.Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Theory]
    [InlineData("shared", AgeStatus.Shared)]
    [InlineData("declined", AgeStatus.Declined)]
    [InlineData("unavailable", AgeStatus.Unavailable)]
    [InlineData("verification_required", AgeStatus.VerificationRequired)]
    [InlineData("recheck_required", AgeStatus.RecheckRequired)]
    [InlineData("bogus", AgeStatus.Unavailable)]
    public void Status_strings_map(string status, AgeStatus expected) =>
        NativeAgeSignal.FromJson($$"""{"status":"{{status}}"}""").Status.ShouldBe(expected);

    [Fact]
    public void Missing_status_is_unavailable() =>
        NativeAgeSignal.FromJson("{}").Status.ShouldBe(AgeStatus.Unavailable);

    [Theory]
    [InlineData("approved", AgeApproval.Approved)]
    [InlineData("pending", AgeApproval.Pending)]
    [InlineData("declined", AgeApproval.Declined)]
    [InlineData("other", AgeApproval.Unknown)]
    public void Approval_strings_map(string approval, AgeApproval expected) =>
        NativeAgeSignal.FromJson($$"""{"approval":"{{approval}}"}""").Approval.ShouldBe(expected);

    [Fact]
    public void Requirements_map_and_ignore_unknown_and_non_strings() =>
        NativeAgeSignal.FromJson("""{"requirements":["parental_consent","adult_notification","x",5]}""")
            .Requirements.ShouldBe(AgeRequirements.ParentalConsent | AgeRequirements.AdultNotification);

    [Fact]
    public void Requirements_that_is_not_an_array_is_ignored() =>
        NativeAgeSignal.FromJson("""{"requirements":"parental_consent"}""").Requirements.ShouldBe(AgeRequirements.None);

    [Theory]
    [InlineData("""{"mandatory":true}""", true)]
    [InlineData("""{"mandatory":"true"}""", false)]
    [InlineData("""{"mandatory":1}""", false)]
    [InlineData("""{"mandatory":false}""", false)]
    public void Mandatory_only_when_literal_true(string json, bool expected) =>
        NativeAgeSignal.FromJson(json).Mandatory.ShouldBe(expected);

    [Theory]
    [InlineData("""{"reuse_session":true}""", true)]
    [InlineData("""{"reuse_session":"true"}""", false)]
    [InlineData("""{}""", false)]
    public void Reuse_session_only_when_literal_true(string json, bool expected) =>
        NativeAgeSignal.FromJson(json).ReuseSession.ShouldBe(expected);
}
