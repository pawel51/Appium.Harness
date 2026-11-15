# CLAUDE.md — Appium.Harness

A test-framework-agnostic helper layer over Appium for .NET mobile UI tests, published as the
`Appium.Harness` NuGet package, plus its app-side companion `Appium.Harness.Maui` (`TestProbe`).
The two are versioned and released separately. Its main consumer is the AlohaAI app
(https://github.com/pawel51/AlohaAI), which for now still carries its own copy of this solution
in `Appium.Harness/` — see "Consumers" below.

## Commands

```sh
dotnet build Appium.Harness.sln
dotnet test  Appium.Harness.sln                       # ~330 tests, ~10 s, no device needed
dotnet test  Appium.Harness.sln --filter "FullyQualifiedName~NativeGestures"
dotnet pack  src/Appium.Harness -c Release -o artifacts
dotnet pack  src/Appium.Harness.Maui -c Release -o artifacts
```

Warnings are errors (`Directory.Build.props`). The library targets `net8.0;net10.0`; build both
before calling something done — an API missing on net8.0 only shows up there.

`Appium.Harness.Maui` targets `net10.0;net10.0-android;net10.0-ios`, so building the solution needs
the `maui` workloads. Without them, build and test the harness alone:
`dotnet test tests/Appium.Harness.Tests`.

## Layout

One assembly, **one public namespace** (`Appium.Harness`), folders by concern:

| Folder | Concern |
|---|---|
| `src/Appium.Harness/Session/` | Options, capability building, opening the driver |
| `src/Appium.Harness/Native/` | `UiDriver` (partials: core, `.Gestures`, `.Assertions`, `.WebView`) and the native internals |
| `src/Appium.Harness/Web/` | `WebUi`, `WebUiOptions`, `WebViewContext` |
| `src/Appium.Harness/Expectations/` | `Expect` / `ExpectCount` engine, failure messages |
| `src/Appium.Harness/Probing/` | `ProbePayload` wire format, `HarnessColor` |
| `src/Appium.Harness/Waiting/` | `Retry` — the only polling primitive |
| `src/Appium.Harness/Diagnostics/` | Screenshots |
| `tests/Appium.Harness.Tests/` | Mirrors the src folders; `TestDoubles/` holds `FakeProbe` |
| `src/Appium.Harness.Maui/` | Separate package: `TestProbe`, `UseTestProbes()` — runs in the app, not the tests |
| `tests/Appium.Harness.Maui.Tests/` | `TestProbe` against real MAUI views on plain `net10.0` |

Folders do not create namespaces. New harness files use `namespace Appium.Harness;`; the MAUI
package uses `namespace Appium.Harness.Maui;`.

## Invariants — do not break these

- **No test-framework dependency** in the library. No NUnit/xUnit/MSTest, no `Assert`. Failures
  are `HarnessAssertionException`. Only `Appium.WebDriver` and `Selenium.WebDriver` are referenced.
- **`Probing/ProbePayload.cs` stays BCL-only.** `Appium.Harness.Maui` compiles this same file
  (and AlohaAI's app link-compiles its own copy), so a Selenium/Appium `using` there breaks the
  MAUI package, and moving or renaming it breaks the link. It is `public` in the harness and
  `internal` in the MAUI package (`#if APPIUM_HARNESS_MAUI`), so a project referencing both
  packages does not see two copies of the type.
- **The MAUI package has no compile-time test gate.** A package is compiled once, in Release, so
  `#if DEBUG` in it means nothing to the app. `TestProbe` publishes only after the app calls
  `UseTestProbes()` from its own test-build `#if`. Keep it that way.
- **Platform branching lives only in** `NativeLocators`, `NativeState` and `NativeGestures`.
  Everything above them is written once for both platforms.
- **Every wait goes through `Retry`** (`Until` / `Poll`): reads at least once, treats
  `WebDriverException` as "not yet", lets every other exception through. Do not write a new
  `while (stopwatch…) Thread.Sleep` loop.
- **Nothing app-specific in the library.** AlohaAI names, attributes and build flags belong in
  the consumer's page objects. Web conventions are `WebUiOptions` settings with defaults.
- **Diagnostics never throw.** Screenshot capture, page source and id listing swallow driver
  errors so they never replace the real failure.

## Tests

Everything is tested without a device, through the internal seams `INativeScreen` (native) and
`IElementProbe` (expectations), and NSubstitute fakes of `IWebDriver` / `IWebElement`.
`InternalsVisibleTo` covers the test assembly and `DynamicProxyGenAssembly2` (NSubstitute).
Pure logic (geometry, parsing, capability building) is `internal static` so it can be tested
directly. Keep timeouts in tests in milliseconds (see `Settings.Fast`).

Device-level verification is the consumer's E2E suite (`tests/AlohaAI.Tests.E2E` in AlohaAI).
A harness change that alters behaviour on a device needs a run there, not just green here.

## Public API and releases

- Public API changes go in the package's changelog — `CHANGELOG.md` for Appium.Harness,
  `src/Appium.Harness.Maui/CHANGELOG.md` for the MAUI package. A removal or signature change is a
  major version bump of `<Version>` in that package's `.csproj`.
- A change to the payload format touches both packages at once; release them together.
- Every public member has an XML doc comment (`GenerateDocumentationFile` + warnings-as-errors
  enforces it). Comments explain *why* — the platform quirk, the failure it prevents.
- README links are absolute GitHub URLs on purpose: nuget.org cannot resolve relative ones.
- Releases are tag-driven through `.github/workflows/publish.yml` (Trusted Publishing); the steps
  are in `docs/RELEASING.md`. Renaming the workflow file breaks the nuget.org policy.

## Consumers

AlohaAI builds against its own in-repo copy (`Appium.Harness/`), not this repository, so a change
made here does not reach it until it is copied across (or AlohaAI moves to the NuGet package).
When changing public API, check these AlohaAI files still build:

- `tests/AlohaAI.Tests.E2E/Android/AlohaAI.Tests.E2E.Android.csproj`
- `tests/AlohaAI.Tests.E2E/iOS/AlohaAI.Tests.E2E.iOS.csproj`
- `src/AlohaAI/Infrastructure/TestProbe.cs` (uses `ProbePayload`, `ProbeProperties`) — to be
  replaced by the `Appium.Harness.Maui` package

## Known issues

Resolved stack bugs (Appium, XCUITest, UIAutomator2, WebDriver) are in `~/.claude/knowledge/`;
harness-specific ones go in `docs/knowledge/`. Search by exact error string before debugging:

```sh
rg -i -- "<exact error string>" docs/knowledge ~/.claude/knowledge
```
