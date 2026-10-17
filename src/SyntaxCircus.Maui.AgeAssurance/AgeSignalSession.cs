namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// In-process, in-memory holder of the last range obtained with prompting allowed. Apple has no silent
/// age-range query, so a silent native check reports <see cref="NativeAgeSignal.ReuseSession"/> and the
/// platform provider re-evaluates the remembered range (with the fresh mandatory/requirements values).
/// Never persisted, logged or transmitted. Not thread-safe by itself; providers serialize checks.
/// </summary>
public sealed class AgeSignalSession
{
    private NativeAgeSignal? _last;

    /// <summary>
    /// Applies session rules to a native signal: a reuse signal is replaced by the last remembered
    /// signal carrying the new <see cref="NativeAgeSignal.Mandatory"/> and
    /// <see cref="NativeAgeSignal.Requirements"/>; otherwise, when <paramref name="allowPrompt"/> is
    /// true, the signal is remembered. Silent non-reuse signals do not update the session.
    /// </summary>
    public NativeAgeSignal Resolve(NativeAgeSignal raw, bool allowPrompt)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.ReuseSession)
        {
            return (_last ?? new NativeAgeSignal()) with
            {
                Mandatory = raw.Mandatory,
                Requirements = raw.Requirements,
                ReuseSession = false,
            };
        }

        if (allowPrompt)
        {
            _last = raw;
        }

        return raw;
    }

    /// <summary>Forgets the remembered range (call when a check is cancelled).</summary>
    public void Clear() => _last = null;
}
