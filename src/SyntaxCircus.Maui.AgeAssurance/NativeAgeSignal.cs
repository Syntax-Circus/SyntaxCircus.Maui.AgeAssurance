using System.Text.Json;

namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>
/// Raw platform signal handed to <see cref="AgeSignalNormalizer"/>. Mirrors the versioned native wire
/// contract: <see cref="Lower"/> is inclusive, <see cref="UpperExclusive"/> exclusive, and a
/// <see langword="null"/> bound means unbounded. Bounds stay in memory only.
/// </summary>
public sealed record NativeAgeSignal
{
    /// <summary>Marker for a wire bound that was present but not a whole number (always rejected by the normalizer).</summary>
    internal const int InvalidBound = -1;

    /// <summary>Outcome of the platform request.</summary>
    public AgeStatus Status { get; init; } = AgeStatus.Unavailable;

    /// <summary>Inclusive lower bound, or <see langword="null"/> if unbounded.</summary>
    public int? Lower { get; init; }

    /// <summary>Exclusive upper bound, or <see langword="null"/> if unbounded.</summary>
    public int? UpperExclusive { get; init; }

    /// <summary>Approval state.</summary>
    public AgeApproval Approval { get; init; } = AgeApproval.Unknown;

    /// <summary>The platform mandates sharing/verification (no manual-entry fallback).</summary>
    public bool Mandatory { get; init; }

    /// <summary>Regulatory obligations.</summary>
    public AgeRequirements Requirements { get; init; } = AgeRequirements.None;

    /// <summary>
    /// The native side performed a silent check with no range of its own (Apple has no silent query);
    /// the last in-process range should be reused. See <see cref="AgeSignalSession"/>.
    /// </summary>
    public bool ReuseSession { get; init; }

    /// <summary>
    /// Parses the native JSON wire payload (<c>status</c>, <c>lower</c>, <c>upper_exclusive</c>,
    /// <c>approval</c>, <c>mandatory</c>, <c>requirements</c>, <c>reuse_session</c>) quietly.
    /// Malformed or non-object input yields an unavailable signal and never throws or logs.
    /// Unknown strings fall back to unavailable/unknown; a bound that is present but not a whole number is
    /// mapped to an out-of-range value so it is rejected rather than treated as unbounded.
    /// </summary>
    public static NativeAgeSignal FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new NativeAgeSignal();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new NativeAgeSignal();
            }

            return new NativeAgeSignal
            {
                Status = ParseStatus(Str(root, "status")),
                Lower = Bound(root, "lower"),
                UpperExclusive = Bound(root, "upper_exclusive"),
                Approval = Str(root, "approval") switch
                {
                    "approved" => AgeApproval.Approved,
                    "pending" => AgeApproval.Pending,
                    "declined" => AgeApproval.Declined,
                    _ => AgeApproval.Unknown,
                },
                Mandatory = IsTrue(root, "mandatory"),
                Requirements = ParseRequirements(root),
                ReuseSession = IsTrue(root, "reuse_session"),
            };
        }
        catch (JsonException)
        {
            return new NativeAgeSignal();
        }
    }

    private static string? Str(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool IsTrue(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static AgeStatus ParseStatus(string? s) => s switch
    {
        "shared" => AgeStatus.Shared,
        "declined" => AgeStatus.Declined,
        "verification_required" => AgeStatus.VerificationRequired,
        "recheck_required" => AgeStatus.RecheckRequired,
        _ => AgeStatus.Unavailable,
    };

    private static int? Bound(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
            && double.IsFinite(d) && d == Math.Floor(d) && Math.Abs(d) <= int.MaxValue)
        {
            return (int)d;
        }

        return InvalidBound;
    }

    private static AgeRequirements ParseRequirements(JsonElement root)
    {
        var result = AgeRequirements.None;
        if (root.TryGetProperty("requirements", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                result |= item.GetString() switch
                {
                    "parental_consent" => AgeRequirements.ParentalConsent,
                    "adult_notification" => AgeRequirements.AdultNotification,
                    _ => AgeRequirements.None,
                };
            }
        }

        return result;
    }
}
