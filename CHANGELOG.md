# Changelog

All notable changes to this package. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the version numbers follow
[Semantic Versioning](https://semver.org/).

## [3.0.0] — 2026-11-17

### Breaking

- **Appium.WebDriver 8.0.1** (from 5.0.0). The harness's own API is unchanged, but
  `UiDriver.Driver`, the `UiDriver` constructor and every method returning `AppiumElement` now
  hand out Appium 8 types. Test code that calls the driver directly gets Appium's own breaking
  changes from 6.x through 8.x — see the
  [Appium .NET client releases](https://github.com/appium/dotnet-client/releases). Consumers
  pinning Appium.WebDriver below 8.0.1 get a NuGet downgrade error. Selenium.WebDriver stays at
  4.40.0, which Appium.WebDriver 8.0.1 accepts.

## [2.1.0] — 2026-11-17

### Changed

- **Selenium.WebDriver 4.40.0** (from 4.21.0). Appium.WebDriver stays at 5.0.0, which accepts it.
  A consumer that pins an older Selenium.WebDriver now gets a NuGet downgrade error and should
  drop the pin or raise it to 4.40.0.
- `WebViewContext.Enter` now selects the window matching the expected URL prefix even
  when the webview context reports a single window handle. Previously a single handle was taken
  as already selected and never switched to, so the session could stay on no window — or on a
  stale one — after the context switch; the expected URL is now checked and logged every time.

## [2.0.0] — 2026-11-15

Moved into a standalone solution (`Appium.Harness/`) with its own `src/`, `tests/`, README and
MIT licence, and reorganised by concern. One namespace, as before — a `using Appium.Harness;`
still reaches every public type.

### Breaking

- **`WebUi.LearnerId` is removed.** It read an app-specific attribute (`data-learner-id`). Read
  any attribute the app publishes on its root with the new `WebUi.RootAttribute(name)`:
  `web.RootAttribute("data-learner-id") ?? ""`.

### Added

- `WebUiOptions.TestIdAttribute`, `AppStateAttribute`, `ReadyState` and `RouteAttribute`, so the
  page contract no longer has to be `data-testid` / `data-app-state="ready"` / `data-route`. The
  defaults are unchanged.
- `WebUi.RootAttribute(string)`.
- Source Link, deterministic builds, the licence file packed into the `.nupkg`.

### Changed

- `UiDriver.WaitOrNull`, `WaitPresent`, `WaitUntilText` and `WaitByText` with a zero timeout now
  look once instead of not at all, matching `Expect(...)`. All waits in both layers go through one
  polling primitive, so the last sleep is cut short at the deadline rather than overrunning it by
  up to one poll interval.
- A failed web screenshot now logs as `[web] screenshot '<name>' failed: …`, the same shape as the
  native one.
- The "no webview context" error no longer names an AlohaAI-specific build property.

### Internal

- `UiDriver` is split into partial files by concern (interaction, gestures, assertions, webview),
  with the platform branching moved into `NativeLocators`, `NativeState` and `NativeGestures`.
- Copies of the screenshot capture, toggle/enabled parsing, simple-id stripping, poll loops and
  web failure diagnostics are each reduced to one.
- Capability building is separated from opening the session, and covered by tests.

## [1.1.0]

Waiting assertions (`Expect`, `ExpectCount`), `HarnessColor`, the probe payload, and failure
screenshots.
