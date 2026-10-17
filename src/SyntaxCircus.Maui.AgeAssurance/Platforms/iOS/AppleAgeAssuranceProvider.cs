using SyntaxCircus.Maui.AgeAssurance.IOS.Binding;

namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Apple Declared Age Range provider (iOS 26+; older systems report unavailable). Port of the Godot iOS plugin: the
/// Swift shim returns the versioned wire JSON, parsed by <see cref="NativeAgeSignal.FromJson"/>. Prompted checks show
/// Apple's sheet; Apple has no silent range query, so silent checks only refresh requirements natively and the shared
/// <see cref="AgeSignalSession"/> re-evaluates the last range from this process. Nothing native runs before
/// <see cref="CheckAsync"/>.
/// </summary>
internal sealed class AppleAgeAssuranceProvider : IAgeAssuranceProvider
{
    private readonly AgeCheckRunner _runner;

    public AppleAgeAssuranceProvider() =>
        _runner = new AgeCheckRunner(new RequestState(), Start, request => SCAgeAssuranceBridge.Cancel(request));

    /// <inheritdoc />
    public Task<AgeCheckResult> CheckAsync(int minimumAge, bool allowPrompt, CancellationToken cancellationToken = default) =>
        _runner.CheckAsync(minimumAge, allowPrompt, cancellationToken);

    private void Start(int request, int minimumAge, bool allowPrompt) =>
        SCAgeAssuranceBridge.Request(
            request,
            minimumAge,
            allowPrompt,
            json => _runner.Complete(request, NativeAgeSignal.FromJson(json?.ToString())));
}
