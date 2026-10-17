# SyntaxCircus.Maui.AgeAssurance

Reusable age signals for .NET MAUI apps on iOS (Apple Declared Age Range) and Android (Google Play Age Signals), normalized into one policy-free result model. No app-specific age policy, account, legal text, persistence or UI. Nothing is called at construction or registration; an explicit `CheckAsync` is the only trigger.

## Install

```
dotnet add package SyntaxCircus.Maui.AgeAssurance
```

Targets `net10.0` (core only, always unavailable), `net10.0-android` (API 24+) and `net10.0-ios` (iOS 15+). The native bindings (Play Age Signals AAR, Declared Age Range Swift shim) ship inside the package; no extra packages to add.

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

## iOS

- Enable the Declared Age Range capability on the Apple App ID and add the entitlement `com.apple.developer.declared-age-range` = `true` to the signed app (`Entitlements.plist`), then regenerate provisioning profiles. A profile without the entitlement fails at runtime and the check reports `Unavailable`.
- Works on iOS 26+ only; the deployment target stays iOS 15 and older systems return `Unavailable`. Build your app with an Xcode that ships the iOS 26.4+ SDK (the prebuilt shim weakly links `DeclaredAgeRange`, so the app still launches on older iOS).
- Apple has no silent age-range query. Prompted checks (`allowPrompt: true`) show Apple's sheet from the foreground key window's top-most view controller and remember the range in memory; silent checks never show UI, re-read the current requirements/mandatory flag and re-evaluate the last range from this process (nothing is persisted, so after a cold start a silent check has no range until the next prompted one). Apple-required sharing cannot fall back to manual entry.
- Cancelling the token abandons the check, returns `Unavailable` and forgets the remembered range.

## Android

- Uses the official Play Age Signals library (`com.google.android.play:age-signals` 0.0.4, bound inside this package; its Java dependencies arrive as normal NuGet dependencies); minSdk 24. Requires the app to be installed from Google Play; installations not managed by Play (sideloaded, emulators without Play) return `Unavailable`.
- Prompted checks call `requestAgeSignalsAccess` (needs a resumed `Platform.CurrentActivity`; without one the result is `Unavailable`) and, when the user shares, `checkAgeSignals`. `VerificationRequired` and `Declined` map from the access result.
- Silent checks only call `checkAgeSignals` after access was shared earlier in the same process, so users who declined optional sharing are not asked repeatedly. If a silent check no longer yields any age bound, the result is `RecheckRequired`. Play's inclusive upper bound is converted to the exclusive bound used internally; `significantChangeStatus` maps to `Approval`. Install ids and the age range source are never read.
- Testing: the 0.0.4 artifact includes `FakeAgeSignalsManager` (`com.google.android.play.agesignals.testing`) and it is part of the bound surface, but this library creates its manager through `AgeSignalsManagerFactory`, so the fake cannot be injected; use the library's `FakeAgeAssuranceProvider` in app tests and Play's testing track (or Android Studio's Play Age Signals test setup) for end-to-end checks.

## Supplement, not replace

Age assurance is a platform signal, not identity verification or a complete compliance product. Use it to supplement your own age screen and policy, not replace them. Required significant-update parental consent / adult notification are exposed as obligations; you must implement those workflows. API data must not be used for advertising, profiling or analytics, and must not be logged or uploaded. Consult the official documentation:

- https://developer.apple.com/support/age-assurance/
- https://developer.android.com/google/play/age-signals/overview

## Build

```
./scripts/build-xcframework.sh          # macOS + Xcode only: builds native/AgeAssuranceBridge/build/AgeAssuranceBridge.xcframework (not committed)
dotnet test tests/SyntaxCircus.Maui.AgeAssurance.Tests
./publish.ps1 -DryRun
```

The net10.0-ios build needs the xcframework to exist (built on a Mac, or copied from one); `net10.0-android` and `net10.0` build anywhere. The unit tests cover the platform-agnostic request runner (single in-flight, timeout, stale replies, cancellation) and the Play status mappings; native behavior needs a device.
