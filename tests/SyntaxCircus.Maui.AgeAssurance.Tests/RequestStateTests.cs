namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// Port of SyntaxCircus.Godot.AgeAssurance RequestStateTest.kt.
public class RequestStateTests
{
    private static NativeAgeSignal Sig(AgeStatus status) => new() { Status = status };

    [Fact]
    public void Cancelled_access_callback_cannot_change_newer_sharing_state()
    {
        var state = new RequestState();
        state.Begin(1);
        state.Cancel(1);
        state.Begin(2);
        state.UpdateShared(2, true).ShouldBeTrue();
        state.UpdateShared(1, false).ShouldBeFalse();
        state.WasShared().ShouldBeTrue();
    }

    [Fact]
    public void Cancelled_or_superseded_results_are_never_delivered()
    {
        var state = new RequestState();
        state.Begin(1);
        state.Begin(2);
        state.Complete(1, Sig(AgeStatus.Declined)).ShouldBeFalse();
        state.Take(2).ShouldBeNull();
        state.Complete(2, Sig(AgeStatus.Shared)).ShouldBeTrue();
        state.Cancel(1);
        state.Take(2)!.Status.ShouldBe(AgeStatus.Shared);
        state.Take(2).ShouldBeNull();
        state.Cancel(2);
        state.Complete(2, Sig(AgeStatus.Declined)).ShouldBeFalse();
        state.Take(2).ShouldBeNull();
    }
}
