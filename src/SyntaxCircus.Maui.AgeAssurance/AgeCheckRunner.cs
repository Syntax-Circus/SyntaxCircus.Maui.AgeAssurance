namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Platform-agnostic request runner shared by the native providers. Port of the request/timeout/stale-reply
/// semantics of the Godot <c>age_provider.gd</c>: one check in flight (a concurrent call is unavailable),
/// a 120 second timeout, globally unique request ids so late replies are discarded, cancellation yields
/// unavailable and clears the remembered session, and a failed or timed-out prompted check replaces the
/// remembered range with an empty one (never leaving a stale range that could authorize).
/// </summary>
internal sealed class AgeCheckRunner
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    private static int s_sequence;

    private readonly RequestState _state;
    private readonly Action<int, int, bool> _request;
    private readonly Action<int> _cancel;
    private readonly AgeSignalSession _session;
    private readonly TimeSpan _timeout;
    private readonly object _gate = new();
    private TaskCompletionSource? _waiter;
    private int _waiterRequest;
    private int _busy;

    /// <param name="state">Request state also visible to the native layer.</param>
    /// <param name="request">Starts a native request (requestId, minimumAge, allowPrompt). Replies go to <see cref="Complete"/>.</param>
    /// <param name="cancel">Retires a native request.</param>
    /// <param name="timeout">Overrides the 120 second timeout (tests).</param>
    internal AgeCheckRunner(RequestState state, Action<int, int, bool> request, Action<int> cancel, TimeSpan? timeout = null)
    {
        _state = state;
        _request = request;
        _cancel = cancel;
        _session = new AgeSignalSession();
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>Delivers a native reply. Returns <see langword="false"/> (and discards it) when <paramref name="request"/> is not the active request.</summary>
    internal bool Complete(int request, NativeAgeSignal signal)
    {
        if (!_state.Complete(request, signal))
        {
            return false;
        }

        TaskCompletionSource? waiter;
        lock (_gate)
        {
            waiter = _waiterRequest == request ? _waiter : null;
        }

        waiter?.TrySetResult();
        return true;
    }

    internal async Task<AgeCheckResult> CheckAsync(int minimumAge, bool allowPrompt, CancellationToken cancellationToken)
    {
        AgeAssuranceGuard.ValidateMinimumAge(minimumAge);

        if (cancellationToken.IsCancellationRequested)
        {
            _session.Clear();
            return AgeCheckResult.Unavailable;
        }

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return AgeCheckResult.Unavailable;
        }

        var request = Interlocked.Increment(ref s_sequence);
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NativeAgeSignal? raw = null;
        var cancelled = false;

        try
        {
            lock (_gate)
            {
                _waiter = waiter;
                _waiterRequest = request;
            }

            _state.Begin(request);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            try
            {
                _request(request, minimumAge, allowPrompt);
                await waiter.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                raw = _state.Take(request);
            }
            catch (OperationCanceledException)
            {
                cancelled = cancellationToken.IsCancellationRequested;
            }
            catch (Exception)
            {
                // A native failure never authorizes anything: treat it like an empty (unavailable) reply.
                raw = null;
            }
        }
        finally
        {
            SafeCancel(request);
            lock (_gate)
            {
                _waiter = null;
                _waiterRequest = 0;
            }

            Volatile.Write(ref _busy, 0);
        }

        if (cancelled)
        {
            _session.Clear();
            return AgeCheckResult.Unavailable;
        }

        // Timeout, failure or no reply behaves like an empty reply (matches the Godot adapter, where a
        // prompted check then replaces the remembered range with an empty one).
        var resolved = _session.Resolve(raw ?? new NativeAgeSignal(), allowPrompt);
        return AgeSignalNormalizer.Normalize(resolved, minimumAge);
    }

    private void SafeCancel(int request)
    {
        try
        {
            _cancel(request);
        }
        catch (Exception)
        {
            // Best effort: retiring a native request must not fail the check.
        }

        _state.Cancel(request);
    }
}
