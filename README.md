# SyntaxCircus.Maui.AgeAssurance

Reusable age signals for .NET MAUI apps on iOS (Apple Declared Age Range) and Android (Google Play Age Signals), normalized into one policy-free result model. No app-specific age policy, account, legal text, persistence or UI. Nothing is called at construction or registration; an explicit `CheckAsync` is the only trigger.

> Status: the pure core (result model, normalizer, restriction state machine, fake provider, DI) is implemented. The native iOS and Android providers land in a later step; until then `AddAgeAssurance()` registers a provider that always returns `Unavailable`. Sections marked _(native)_ describe the finished library.

## Install

```
dotnet add package SyntaxCircus.Maui.AgeAssurance
```

Targets `net10.0` (core only, always unavailable), `net10.0-android` (API 24+) and `net10.0-ios` (iOS 15+).

## Usage

```csharp
// MauiProgram.cs
builder.Services.AddAgeAssurance();

// Anywhere, via DI
public sealed class AgeGate(IAgeAssuranceProvider age)
{
    public async Task<AgeRestriction> CheckAsync(AgeRestriction persisted, bool initialLaunchOrRecheck)
    {
        // Prompt allowed on first launch or explicit Recheck; silent on foreground.
        AgeCheckResult result = await age.CheckAsync(minimumAge: 13, allowPrompt: initialLaunchOrRecheck);

        // Persist only this category (never a platform range).
        return AgeRestrictionTransitions.Next(persisted, result);
    }
}
```

`CheckAsync(minimumAge, allowPrompt, cancellationToken)`: `minimumAge` must be 1..120. At most one check is in flight (a concurrent call returns `Unavailable`); a check is abandoned after 120 seconds and stale replies are discarded; cancelling the token returns `Unavailable` and clears any remembered range. Check before honoring a cached acceptance or enabling optional services.

## Result model

| Property | Values | Meaning |
|---|---|---|
| `Status` | `Shared`, `Declined`, `Unavailable`, `VerificationRequired`, `RecheckRequired` | Outcome of the platform request |
| `Eligibility` | `Eligible`, `BelowMinimum`, `Unknown` | Whether the whole shared range is at/above or below your minimum. `Unknown` is never proof of adulthood |
| `Restriction` | `None`, `BelowMinimum`, `VerificationRequired`, `ApprovalRequired`, `RecheckRequired` | Category to persist; feed it through `AgeRestrictionTransitions.Next` |
| `Approval` | `Unknown`, `Approved`, `Pending`, `Declined` | Parental approval, independent of age |
| `Requirements` | flags `ParentalConsent`, `AdultNotification` | Regulatory obligations (not pending consent) |

Age bounds are never exposed. A successful silent Google response that loses previously shared bounds yields `RecheckRequired`; a confirmed optional decline clears only that recheck restriction. Failures cannot clear a known restriction, and eligible age alone cannot resolve a pending approval.

For tests, use `FakeAgeAssuranceProvider` (queued or fixed `NativeAgeSignal`s run through the real normalizer, recorded `Calls`, `Hold`/`Finish`).

## iOS _(native)_

- Enable the Declared Age Range capability on the Apple App ID and add the entitlement `com.apple.developer.declared-age-range` = `true` to the signed app, then regenerate provisioning profiles. A profile without it fails at runtime.
- Needs an iOS 26.4+ SDK to build; the framework is linked weakly and deployment stays iOS 15, so older OS versions return `Unavailable`.
- Apple has no silent query: prompted checks obtain a new range, silent checks re-evaluate the last range from this process plus current requirements. Apple-required sharing cannot fall back to manual entry.

## Android _(native)_

- Uses the official Play Age Signals library (`com.google.android.play:age-signals`), added transitively as a NuGet/Maven dependency; minSdk 24. Installations not managed by Play may return `Unavailable`.
- Silent checks retrieve previously shared signals without asking again. Do not repeatedly ask users who declined optional sharing.

## Supplement, not replace

Age assurance is a platform signal, not identity verification or a complete compliance product. Use it to supplement your own age screen and policy, not replace them. Required significant-update parental consent / adult notification are exposed as obligations; you must implement those workflows. API data must not be used for advertising, profiling or analytics, and must not be logged or uploaded. Consult the official documentation:

- https://developer.apple.com/support/age-assurance/
- https://developer.android.com/google/play/age-signals/overview

## Build

```
dotnet test tests/SyntaxCircus.Maui.AgeAssurance.Tests
./publish.ps1 -DryRun
```
