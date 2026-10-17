namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Mirrors SyntaxCircus.Godot.AgeAssurance tests/test_age_assurance.gd (fake provider uses the real normalizer,
// absent native plugin falls back to unavailable) and fake_age_provider.gd (calls, response, hold/finish).
public class FakeAgeAssuranceProviderTests
{
    [Fact]
    public async Task Injectable_fake_uses_real_normalizer_and_records_calls()
    {
        var fake = new FakeAgeAssuranceProvider
        {
            Response = new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 13, UpperExclusive = 16 },
        };

        var result = await fake.CheckAsync(13, true, TestContext.Current.CancellationToken);

        result.Eligibility.ShouldBe(AgeEligibility.Eligible);
        fake.Calls.ShouldBe([new AgeCheckCall(13, true)]);
    }

    [Fact]
    public async Task Default_response_is_unavailable()
    {
        var result = await new FakeAgeAssuranceProvider().CheckAsync(13, false, TestContext.Current.CancellationToken);
        result.ShouldBe(AgeCheckResult.Unavailable);
    }

    [Fact]
    public async Task Queued_signals_are_consumed_in_order_then_fall_back_to_response()
    {
        var fake = new FakeAgeAssuranceProvider
        {
            Response = new NativeAgeSignal { Status = AgeStatus.Declined },
        };
        fake.Enqueue(new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 18 });
        fake.Enqueue(new NativeAgeSignal { Status = AgeStatus.VerificationRequired });
        var ct = TestContext.Current.CancellationToken;

        (await fake.CheckAsync(13, true, ct)).Eligibility.ShouldBe(AgeEligibility.Eligible);
        (await fake.CheckAsync(13, true, ct)).Restriction.ShouldBe(AgeRestriction.VerificationRequired);
        (await fake.CheckAsync(13, true, ct)).Status.ShouldBe(AgeStatus.Declined);
        fake.Calls.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Held_check_completes_only_after_finish()
    {
        var fake = new FakeAgeAssuranceProvider
        {
            Hold = true,
            Response = new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 18 },
        };

        var pending = fake.CheckAsync(13, true, TestContext.Current.CancellationToken);

        pending.IsCompleted.ShouldBeFalse();
        fake.Calls.Count.ShouldBe(1);
        fake.Finish();
        (await pending).Eligibility.ShouldBe(AgeEligibility.Eligible);
        fake.Hold.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelling_a_held_check_returns_unavailable()
    {
        var fake = new FakeAgeAssuranceProvider
        {
            Hold = true,
            Response = new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 18 },
        };
        using var cts = new CancellationTokenSource();

        var pending = fake.CheckAsync(13, true, cts.Token);
        await cts.CancelAsync();

        (await pending).ShouldBe(AgeCheckResult.Unavailable);
    }

    [Fact]
    public async Task Absent_native_plugin_is_a_supported_fallback()
    {
        var result = await new UnavailableAgeAssuranceProvider().CheckAsync(13, false, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AgeStatus.Unavailable);
    }

    [Fact]
    public void Enqueue_null_throws() =>
        Should.Throw<ArgumentNullException>(() => new FakeAgeAssuranceProvider().Enqueue(null!));
}
