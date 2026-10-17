namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Port of SyntaxCircus.Godot.AgeAssurance tests/test_native_adapter.gd and the request/timeout/stale-reply
// semantics of age_provider.gd, against the pure AgeCheckRunner.
public class AgeCheckRunnerTests
{
    private sealed class Bridge
    {
        public int Active;
        public int Requests;
        public int LastRequest;
        public int Cancels;
        public bool Hold;
        public NativeAgeSignal? Answer = new() { Status = AgeStatus.Shared, Lower = 18 };
        public AgeCheckRunner Runner = null!;

        public AgeCheckRunner Create(TimeSpan? timeout = null)
        {
            var state = new RequestState();
            Runner = new AgeCheckRunner(state, Request, Cancel, timeout);
            return Runner;
        }

        private void Request(int id, int minimum, bool prompt)
        {
            Active = id;
            LastRequest = id;
            Requests++;
            if (!Hold && Answer is not null)
            {
                Runner.Complete(id, Answer);
            }
        }

        private void Cancel(int id)
        {
            if (Active == id)
            {
                Active = 0;
                Cancels++;
            }
        }
    }

    [Fact]
    public void Construction_never_starts_native_sdk()
    {
        var bridge = new Bridge();
        bridge.Create();
        bridge.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task Native_signal_is_normalized_and_request_retired()
    {
        var bridge = new Bridge();
        var runner = bridge.Create();
        (await runner.CheckAsync(13, true, TestContext.Current.CancellationToken)).Eligibility.ShouldBe(AgeEligibility.Eligible);
        bridge.Cancels.ShouldBe(1);
    }

    [Fact]
    public async Task Silent_apple_result_reevaluates_only_the_last_completed_range()
    {
        var bridge = new Bridge();
        var runner = bridge.Create();
        await runner.CheckAsync(13, true, TestContext.Current.CancellationToken);
        bridge.Answer = new NativeAgeSignal { ReuseSession = true };
        (await runner.CheckAsync(16, false, TestContext.Current.CancellationToken)).Eligibility.ShouldBe(AgeEligibility.Eligible);
    }

    [Fact]
    public async Task Cancelled_requests_cannot_repopulate_the_session_cache()
    {
        var bridge = new Bridge();
        var runner = bridge.Create();
        await runner.CheckAsync(13, true, TestContext.Current.CancellationToken);

        bridge.Hold = true;
        using var cts = new CancellationTokenSource();
        var pending = runner.CheckAsync(13, true, cts.Token);
        await cts.CancelAsync();
        (await pending).ShouldBe(AgeCheckResult.Unavailable);

        bridge.Hold = false;
        bridge.Answer = new NativeAgeSignal { ReuseSession = true };
        (await runner.CheckAsync(13, false, TestContext.Current.CancellationToken)).Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Fact]
    public async Task Empty_reply_cannot_authorize()
    {
        var bridge = new Bridge { Answer = new NativeAgeSignal() };
        var runner = bridge.Create();
        (await runner.CheckAsync(13, true, TestContext.Current.CancellationToken)).Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Fact]
    public async Task Expired_check_cancels_native_request_and_next_request_cannot_receive_previous_answer()
    {
        var bridge = new Bridge { Hold = true };
        var runner = bridge.Create(TimeSpan.FromMilliseconds(1));
        var result = await runner.CheckAsync(13, true, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(AgeStatus.Unavailable);
        bridge.Active.ShouldBe(0);

        var previous = bridge.LastRequest;
        bridge.Hold = false;
        bridge.Answer = new NativeAgeSignal { Status = AgeStatus.VerificationRequired };
        var next = bridge.Create(); // fresh runner shares the static sequence
        (await next.CheckAsync(13, true, TestContext.Current.CancellationToken)).Restriction.ShouldBe(AgeRestriction.VerificationRequired);
        bridge.LastRequest.ShouldNotBe(previous);
    }

    [Fact]
    public async Task Late_reply_after_timeout_is_discarded()
    {
        var bridge = new Bridge { Hold = true };
        var runner = bridge.Create(TimeSpan.FromMilliseconds(1));
        await runner.CheckAsync(13, true, TestContext.Current.CancellationToken);
        runner.Complete(bridge.LastRequest, new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 18 }).ShouldBeFalse();
    }

    [Fact]
    public async Task Timed_out_prompted_check_replaces_remembered_range_with_empty()
    {
        var bridge = new Bridge();
        var runner = bridge.Create(TimeSpan.FromMilliseconds(20));
        await runner.CheckAsync(13, true, TestContext.Current.CancellationToken);
        bridge.Hold = true;
        await runner.CheckAsync(13, true, TestContext.Current.CancellationToken);
        bridge.Hold = false;
        bridge.Answer = new NativeAgeSignal { ReuseSession = true };
        (await runner.CheckAsync(13, false, TestContext.Current.CancellationToken)).Eligibility.ShouldBe(AgeEligibility.Unknown);
    }

    [Fact]
    public async Task Concurrent_call_returns_unavailable_without_touching_native()
    {
        var bridge = new Bridge { Hold = true };
        var runner = bridge.Create();
        var first = runner.CheckAsync(13, true, TestContext.Current.CancellationToken);
        (await runner.CheckAsync(13, true, TestContext.Current.CancellationToken)).ShouldBe(AgeCheckResult.Unavailable);
        bridge.Requests.ShouldBe(1);

        runner.Complete(bridge.LastRequest, new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 18 }).ShouldBeTrue();
        (await first).Eligibility.ShouldBe(AgeEligibility.Eligible);
    }

    [Fact]
    public async Task Native_exception_yields_unavailable_and_releases_the_slot()
    {
        var state = new RequestState();
        var fail = true;
        AgeCheckRunner? runner = null;
        runner = new AgeCheckRunner(state, (id, _, _) =>
        {
            if (fail)
            {
                throw new InvalidOperationException("boom");
            }

            runner!.Complete(id, new NativeAgeSignal { Status = AgeStatus.Shared, Lower = 18 });
        }, _ => { });

        (await runner.CheckAsync(13, true, TestContext.Current.CancellationToken)).Status.ShouldBe(AgeStatus.Unavailable);
        fail = false;
        (await runner.CheckAsync(13, true, TestContext.Current.CancellationToken)).Eligibility.ShouldBe(AgeEligibility.Eligible);
    }

    [Fact]
    public async Task Invalid_minimum_age_throws()
    {
        var bridge = new Bridge();
        var runner = bridge.Create();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => runner.CheckAsync(0, true, TestContext.Current.CancellationToken));
    }
}
