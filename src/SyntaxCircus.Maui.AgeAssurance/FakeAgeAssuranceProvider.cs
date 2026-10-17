namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>One recorded call to <see cref="FakeAgeAssuranceProvider.CheckAsync"/>.</summary>
/// <param name="MinimumAge">The minimum age passed.</param>
/// <param name="AllowPrompt">Whether prompting was allowed.</param>
public sealed record AgeCheckCall(int MinimumAge, bool AllowPrompt);

/// <summary>
/// Test double that runs configured <see cref="NativeAgeSignal"/>s through the real
/// <see cref="AgeSignalNormalizer"/> and records calls. Supports held asynchronous results.
/// Public so consuming apps' tests can use it.
/// </summary>
public sealed class FakeAgeAssuranceProvider : IAgeAssuranceProvider
{
    private readonly Queue<NativeAgeSignal> _queue = new();
    private readonly List<AgeCheckCall> _calls = [];
    private TaskCompletionSource? _gate;

    /// <summary>Signal used when the queue is empty. Defaults to unavailable.</summary>
    public NativeAgeSignal Response { get; set; } = new();

    /// <summary>When true, checks wait until <see cref="Finish"/> is called.</summary>
    public bool Hold
    {
        get => _gate is not null;
        set => _gate = value ? _gate ?? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
    }

    /// <summary>Calls recorded so far, in order.</summary>
    public IReadOnlyList<AgeCheckCall> Calls => _calls;

    /// <summary>Queues a one-shot signal consumed (FIFO) before <see cref="Response"/> is used.</summary>
    public void Enqueue(NativeAgeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        _queue.Enqueue(signal);
    }

    /// <summary>Stops holding and releases any waiting checks.</summary>
    public void Finish()
    {
        var gate = _gate;
        _gate = null;
        gate?.TrySetResult();
    }

    /// <inheritdoc />
    public async Task<AgeCheckResult> CheckAsync(int minimumAge, bool allowPrompt, CancellationToken cancellationToken = default)
    {
        AgeAssuranceGuard.ValidateMinimumAge(minimumAge);
        _calls.Add(new AgeCheckCall(minimumAge, allowPrompt));

        var gate = _gate;
        if (gate is not null)
        {
            try
            {
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return AgeCheckResult.Unavailable;
            }
        }

        var signal = _queue.Count > 0 ? _queue.Dequeue() : Response;
        return AgeSignalNormalizer.Normalize(signal, minimumAge);
    }
}
