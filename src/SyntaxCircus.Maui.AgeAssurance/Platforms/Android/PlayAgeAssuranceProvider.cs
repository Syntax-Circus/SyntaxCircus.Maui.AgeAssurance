using Android.App;
using Android.Gms.Tasks;
using Com.Google.Android.Play.Agesignals;
using Com.Google.Android.Play.Agesignals.Model;
using Java.Interop;
using Microsoft.Maui.ApplicationModel;

namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Google Play Age Signals provider. Explicit requests only: nothing is created before
/// <see cref="CheckAsync"/>. Prompted checks call <c>requestAgeSignalsAccess</c> and, when the user shares,
/// <c>checkAgeSignals</c>; silent checks only call <c>checkAgeSignals</c> after access was shared earlier in
/// this process. Port of the Godot Android plugin. Never reads install ids or the age range source.
/// </summary>
internal sealed class PlayAgeAssuranceProvider : IAgeAssuranceProvider
{
    // The pure mapper keeps its own copies of the Play constants so it stays testable on net10.0; fail fast if the
    // bound Java constants ever differ.
    private static readonly bool s_constantsMatch =
        PlayAgeSignalMapper.AccessShared == IAgeSignalsStatus.Shared
        && PlayAgeSignalMapper.AccessNotShared == IAgeSignalsStatus.NotShared
        && PlayAgeSignalMapper.AccessVerificationRequired == IAgeSignalsStatus.VerificationRequired
        && PlayAgeSignalMapper.ChangeApproved == ISignificantChangeStatus.Approved
        && PlayAgeSignalMapper.ChangePending == ISignificantChangeStatus.Pending
        && PlayAgeSignalMapper.ChangeDeclined == ISignificantChangeStatus.Declined;

    private readonly RequestState _state = new();
    private readonly AgeCheckRunner _runner;

    public PlayAgeAssuranceProvider()
    {
        if (!s_constantsMatch)
        {
            throw new InvalidOperationException("Play Age Signals constants changed; update PlayAgeSignalMapper.");
        }

        _runner = new AgeCheckRunner(_state, Start, _ => { });
    }

    /// <inheritdoc />
    public Task<AgeCheckResult> CheckAsync(int minimumAge, bool allowPrompt, System.Threading.CancellationToken cancellationToken = default) =>
        _runner.CheckAsync(minimumAge, allowPrompt, cancellationToken);

    private void Start(int request, int minimumAge, bool allowPrompt)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            _runner.Complete(request, new NativeAgeSignal());
            return;
        }

        activity.RunOnUiThread(() =>
        {
            if (!_state.IsActive(request))
            {
                return;
            }

            try
            {
                Run(activity, request, allowPrompt);
            }
            catch (Exception)
            {
                _runner.Complete(request, new NativeAgeSignal());
            }
        });
    }

    private void Run(Activity activity, int request, bool allowPrompt)
    {
        var manager = AgeSignalsManagerFactory.Create(activity.ApplicationContext!);

        if (!allowPrompt)
        {
            if (_state.WasShared())
            {
                Retrieve(manager, request, allowPrompt);
            }
            else
            {
                _runner.Complete(request, new NativeAgeSignal());
            }

            return;
        }

        var accessRequest = AgeSignalsAccessRequest.InvokeBuilder()!.SetActivity(activity)!.Build()!;
        manager.RequestAgeSignalsAccess(accessRequest)!
            .AddOnSuccessListener(new Listener(result =>
            {
                var access = result.JavaCast<AgeSignalsAccessResult>();
                var status = access.AgeSignalsStatus()?.IntValue();
                if (!_state.UpdateShared(request, status == PlayAgeSignalMapper.AccessShared))
                {
                    return;
                }

                if (status == PlayAgeSignalMapper.AccessShared)
                {
                    Retrieve(manager, request, allowPrompt);
                }
                else
                {
                    _runner.Complete(request, PlayAgeSignalMapper.ForAccessDenied(status));
                }
            }))!
            .AddOnFailureListener(new Listener(() => _runner.Complete(request, new NativeAgeSignal())));
    }

    private void Retrieve(IAgeSignalsManager manager, int request, bool allowPrompt)
    {
        if (!_state.IsActive(request))
        {
            return;
        }

        manager.CheckAgeSignals(AgeSignalsRequest.InvokeBuilder()!.Build()!)!
            .AddOnSuccessListener(new Listener(result =>
            {
                var signals = result.JavaCast<AgeSignalsResult>();
                // Deliberately never read InstallId() or AgeRangeSource().
                _runner.Complete(
                    request,
                    PlayAgeSignalMapper.ForResult(
                        signals.AgeLower()?.IntValue(),
                        signals.AgeUpper()?.IntValue(),
                        signals.SignificantChangeStatus()?.IntValue(),
                        allowPrompt));
            }))!
            .AddOnFailureListener(new Listener(() => _runner.Complete(request, new NativeAgeSignal())));
    }

    private sealed class Listener : Java.Lang.Object, IOnSuccessListener, IOnFailureListener
    {
        private readonly Action<Java.Lang.Object>? _onSuccess;
        private readonly Action? _onFailure;

        public Listener(Action<Java.Lang.Object> onSuccess) => _onSuccess = onSuccess;

        public Listener(Action onFailure) => _onFailure = onFailure;

        public void OnSuccess(Java.Lang.Object? result)
        {
            if (result is not null)
            {
                _onSuccess?.Invoke(result);
            }
        }

        public void OnFailure(Java.Lang.Exception e) => _onFailure?.Invoke();
    }
}
