# Age assurance library (MAUI)

Use feature branches in this checkout, never worktrees. Port of `SyntaxCircus.Godot.AgeAssurance`; keep the normalizer and restriction rules behaviorally identical to it.

Library rules:

- No app-specific paths, legal text, persistence, UI or minimum ages in the library. The app passes `minimumAge` (1..120) on every check and persists only the `AgeRestriction` category.
- Native APIs initialize only on an explicit `CheckAsync`, never at construction or DI registration.
- Never log, store or send age bounds or platform install identifiers. `AgeCheckResult` must not expose bounds. Bounds live only in memory (`NativeAgeSignal`, `AgeSignalSession`).
- Apple has no documented silent age-range query: silent iOS checks inspect requirements and reuse the last in-process range via `AgeSignalSession`. Cancellation clears that session.
- Android silent checks run only after access was shared in-process; do not repeatedly ask users who declined optional sharing.
- Keep eligibility, mandatory verification, pending/declined approval and regulatory requirements distinct. Unknown is never proof of adulthood.
- One check in flight (concurrent call returns Unavailable), 120 second timeout, stale replies discarded.
- Tests: `dotnet test tests/SyntaxCircus.Maui.AgeAssurance.Tests` (net10.0 core only; platform providers need a Mac/device). Allman braces, TreatWarningsAsErrors, XML docs on public API.
