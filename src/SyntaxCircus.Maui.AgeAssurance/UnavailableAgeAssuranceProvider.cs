namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>Provider for platforms without an age-signal API. Always returns <see cref="AgeCheckResult.Unavailable"/>.</summary>
public sealed class UnavailableAgeAssuranceProvider : IAgeAssuranceProvider
{
    /// <inheritdoc />
    public Task<AgeCheckResult> CheckAsync(int minimumAge, bool allowPrompt, CancellationToken cancellationToken = default)
    {
        AgeAssuranceGuard.ValidateMinimumAge(minimumAge);
        return Task.FromResult(AgeCheckResult.Unavailable);
    }
}
