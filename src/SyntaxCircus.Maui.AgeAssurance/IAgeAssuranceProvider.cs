namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Explicitly invoked age check. Nothing happens at construction; native APIs initialize only on
/// <see cref="CheckAsync"/>.
/// </summary>
/// <remarks>
/// Semantics shared by all implementations: at most one check is in flight (a concurrent call returns
/// <see cref="AgeCheckResult.Unavailable"/>); a check that exceeds 120 seconds is abandoned and returns
/// unavailable, and its late reply is discarded; cancelling the token returns unavailable and forgets any
/// remembered session range. <c>minimumAge</c> is supplied by the app and must be within
/// 1..120 (<see cref="ArgumentOutOfRangeException"/> otherwise). Call with prompting allowed on initial
/// launch or explicit recheck, and silently on foreground.
/// </remarks>
public interface IAgeAssuranceProvider
{
    /// <summary>Runs an age check against <paramref name="minimumAge"/>.</summary>
    /// <param name="minimumAge">The app's minimum age, 1..120.</param>
    /// <param name="allowPrompt">Whether the platform may show its consent UI.</param>
    /// <param name="cancellationToken">Cancels the check; cancellation yields unavailable rather than throwing.</param>
    Task<AgeCheckResult> CheckAsync(int minimumAge, bool allowPrompt, CancellationToken cancellationToken = default);
}

internal static class AgeAssuranceGuard
{
    internal const int MinimumAgeLowerLimit = 1;
    internal const int MinimumAgeUpperLimit = 120;

    internal static void ValidateMinimumAge(int minimumAge)
    {
        if (minimumAge is < MinimumAgeLowerLimit or > MinimumAgeUpperLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAge), minimumAge, "Minimum age must be within 1..120.");
        }
    }
}
