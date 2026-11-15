# Appium.Harness

A thin, **test-framework-agnostic** helper layer over [Appium](https://appium.io) for .NET mobile
UI tests. Element lookup by automation id, resilient waits, precise no-fling gestures, embedded
WebView support, and local-or-cloud session setup.

Built for [.NET MAUI](https://learn.microsoft.com/dotnet/maui/) — where
[`AutomationId`](https://learn.microsoft.com/dotnet/maui/fundamentals/accessibility) maps to the
Android `resource-id` and the iOS accessibility id — but nothing here is MAUI-specific.

**New to this?** Start with **[the guide](https://github.com/pawel51/Appium.Harness/blob/main/docs/GUIDE.md)**, which walks from an empty project to a
passing test. This file is the reference: what each type is and why it exists.

---

## The three types, and why there are three

A hybrid app is two automation worlds behind one screen. The native tree is driven by
UIAutomator2 or XCUITest and addressed by automation id; an embedded WebView is a DOM driven by
chromedriver or Safari's inspector and addressed by CSS. **One Appium session can be in only one
of them at a time**, and switching is a global change to the driver.

That shape is what the three types encode:

```
        NATIVE_APP context                     WEBVIEW_* context
   ┌───────────────────────────┐          ┌───────────────────────────┐
   │ UiDriver                  │          │ WebUi                     │
   │ AutomationId → native tree│          │ data-testid → DOM         │
   │ taps, gestures, alerts    │          │ taps, fills, readiness    │
   └─────────────┬─────────────┘          └─────────────┬─────────────┘
                 │                                      │
                 │   EnterWebView()  ───────────────►   │
                 │        WebViewContext: a scope       │
                 │   ◄───────────────  Dispose()        │
                 └──────────────────────────────────────┘
              one AppiumDriver answers both — one at a time
```

| Type | Lives in | Addresses | Exists because |
|---|---|---|---|
| [`UiDriver`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Native/UiDriver.cs) | native context | `AutomationId` | the native tree needs waits, gestures and platform smoothing |
| [`WebViewContext`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Web/WebViewContext.cs) | the seam | — | the context switch is global, so it needs a scope that always unwinds |
| [`WebUi`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Web/WebUi.cs) | webview context | `data-testid` | a DOM needs different primitives, and a SPA needs a readiness signal |

A purely native app uses `UiDriver` alone and can ignore the other two.

---

## `UiDriver` — the native layer

The type a test spends most of its life in. Wraps an `AppiumDriver` and adds the things every
mobile suite ends up writing badly at least once.

**Automation ids first.** `ByAutomationId` resolves to the right locator per platform, so a test
never spells out `resource-id` or an iOS predicate. Keep the id strings in one shared constants
class (see [the guide](https://github.com/pawel51/Appium.Harness/blob/main/docs/GUIDE.md#5-make-the-app-testable)) and a rename becomes a compile error on
both sides instead of a runtime miss.

**Waits that say where the app actually was.** `Wait` throws carrying
`VisibleAutomationIds()` — the ids genuinely on screen at that moment. A bare
`NoSuchElementException` tells you an element was absent; it does not tell you the app was still
on the previous screen, which is nearly always the real answer.

**Gestures are controlled drags, not flings.** Android uses UIAutomator2's `scrollGesture` /
`swipeGesture`; iOS uses XCUITest's `dragFromToForDuration`, because
[`mobile: scroll`](https://appium.github.io/appium-xcuitest-driver/latest/reference/execute-methods/#mobile-scroll)
needs an in-tree anchor and is unreliable without one. Scrolling therefore stops where it was
asked to, and a tap immediately afterwards is not swallowed by inertia. Endpoints are clamped
away from the screen edges, where a drag is claimed by the OS's own gestures and silently does
nothing.

**Platform differences it absorbs.** These are the ones that otherwise produce a test which
passes on one platform and silently inverts on the other:

- `IsToggledOn` reads `checked` on Android and `value` on iOS.
- `WaitByText` uses a UiSelector on Android and an NSPredicate on iOS — both **exact** matches, so
  asking for `"Reset"` cannot hit a `"Reset Progress"` label nearby.

**One difference it deliberately does not hide.** `Back()` is
`Driver.Navigate().Back()`, which presses Android's hardware key. **iOS has no such key: XCUITest
accepts the command, does nothing, and reports success.** It is left as-is because there is no
generic iOS equivalent — a page that hides the system navigation bar must be left through its own
back control. Page objects own that knowledge; see
[`AlohaAiApp.LeaveCurrentPushedPage`](https://github.com/pawel51/AlohaAI/blob/main/tests/AlohaAI.Tests.E2E/Shared/AlohaAiApp.cs) for the
worked version. ⚠ [Risks](#risks)

### API

| Area | Members |
|---|---|
| **Finding** | `ByAutomationId`, `Find`, `Wait`, `WaitOrNull`, `WaitPresent`, `Exists`, `AnyIdMatches`, `VisibleAutomationIds`, `SafePageSource` |
| **Interacting** | `Tap`, `TapIfPresent`, `Fill`, `TextOf`, `IsEnabled`, `IsToggledOn`, `WaitUntilText`, `WaitByText`, `TapByText`, `AcceptAlert`, `HideKeyboard`, `Back`, `RestartApp` |
| **Scrolling** | `ScrollTo`, `ScrollUpTo`, `ScrollToOrNull`, `ScrollUpToOrNull` |
| **Gestures** | `SwipeUp`, `SwipeDown`, `SwipeLeft`, `SwipeRight`, `Swipe`, `SwipeFrom` |
| **Hybrid** | `EnterWebView` |
| **Asserting** | `Expect(id)`, `ExpectCount(idRegex, scroll)` — see [Assertions](#assertions) |
| **Reading published values** | `CountIds`, `PropertyOf`, `Properties`, `ColorOf` |
| **Diagnostics** | `TryCaptureScreenshot`, and the `Log` hook on the options |
| **Escape hatch** | `Driver` — the raw `AppiumDriver`, for anything not covered |

`AcceptAlert(buttonText)` taps the button you name and only falls back to
`Alert().Accept()` when no button with that label is in the tree. "Accept" is platform-defined —
Android presses the positive button, iOS the last one — so on a *Cancel / Reset* pair, delegating
to the platform can silently press Cancel while the alert closes and the call reports success.

---

## `WebViewContext` — the seam

Switches a live session into an embedded WebView and puts it back on disposal.

```csharp
using (var web = ui.EnterWebView("http://10.0.2.2:5081", "com.example.app"))
{
    web.Web.Tap("mark-complete");        // CSS, against the live DOM
}
// back in the native tree here
```

**Why a disposable scope rather than two calls.** Setting the context is global to the driver. If
an assertion throws between "enter" and "leave", every later step in that test — and the failure
diagnostics themselves — would run against a DOM instead of the native tree. `using` makes the
unwind unconditional.

What `Enter` does, in order:

1. **Polls** `IContextAware.Contexts` until a `WEBVIEW_*` name appears. Polls rather than reads
   once, because the context registers only after the page starts loading.
2. **Chooses** one — preferring a name containing the app package. Android names contexts by
   package (`WEBVIEW_com.example.app`), iOS by handle (`WEBVIEW_24130.1`).
3. **Selects the window** whose URL starts with the expected prefix. A single context can expose
   several handles, and the wrong one yields an empty `<html>` — which presents as every selector
   failing at once.
4. **Waits for readiness** (`data-app-state`), logging rather than throwing if it never arrives,
   so the test's own wait produces the better message.

`Dispose` is deliberately failure-tolerant and idempotent: restoring the context is a courtesy to
whatever runs next, never a reason to mask the real failure.

**If no webview context ever appears, the cause is almost never Appium.** It is that the app has
no *inspectable* WebView — no DevTools socket to attach to. The exception names the contexts that
*were* available, so the message says that rather than "no such element". Fixing it is an app
build concern: see [Android](https://github.com/pawel51/AlohaAI/blob/main/tests/AlohaAI.Tests.E2E/Android/WEBVIEW.md) or
[iOS](https://github.com/pawel51/AlohaAI/blob/main/tests/AlohaAI.Tests.E2E/iOS/WEBVIEW.md).

---

## `WebUi` — the DOM layer

`UiDriver`'s twin for web content, with the same vocabulary — `Wait`, `Exists`, `Tap`, `Fill`,
`TextOf` — so moving between a native page object and a web one is not a change of language.

**It takes `IWebDriver`, not `AppiumDriver`.** That one signature is the point: `AppiumDriver`
derives from `WebDriver`, so the same page objects drive an embedded WebView on a device and, if
you ever want it, a desktop browser — with no second implementation to keep in step.

**Waits, never sleeps.** A SPA has no page loads, so there is nothing to wait on by default:
`document.readyState` reports that the HTML shell arrived, which says nothing about whether the
app has rendered. So the app publishes its own state and `WebUi` reads it:

| Attribute on the root element | Read by | Means |
|---|---|---|
| `data-app-state` | `AppState`, `WaitForAppReady` | `booting` / `loading` / `ready` / `error` |
| `data-route` | `CurrentRoute`, `WaitForRoute` | the current route name, so a test asserts on a name rather than parsing a URL |

Those names, the `ready` value and the `data-testid` attribute are defaults, not requirements:
an app that spells them differently sets `TestIdAttribute`, `AppStateAttribute`, `ReadyState` and
`RouteAttribute` on [`WebUiOptions`](#options). Anything else the app publishes on its root — a
user id, a feature flag — is read with `RootAttribute("data-user-id")`.

`WaitUntil(Func<bool>)` covers what readiness cannot: *"the app looks idle"* is not the same
question as *"the thing I just asked for has happened"*. A click that fires a request is only
counted once the client issues it — a microtask later — so polling readiness immediately after a
click can observe the **previous** `ready` and return before the write has begun. Wait for the
consequence instead (a button going disabled, a value changing).

**Two DOM-specific hazards it handles for you:**

- **Stale handles.** A handle is a pointer to a specific node, and a reactive framework replaces
  nodes rather than mutating them. Every interaction goes through an internal re-locate-and-retry,
  which is safe because the driver checks that an element is attached *before* it acts — a stale
  exception means the click or keystroke never landed, so repeating it cannot double it.
- **Off-screen text reads as empty.** [WebDriver defines an element's text as its *rendered*
  text](https://www.w3.org/TR/webdriver/#get-element-text): an element outside the viewport is
  still found, still reports itself displayed, and returns `""`. `TextOf` and `WaitUntilText`
  therefore scroll into view first. WKWebView is markedly stricter about this than Chrome.

### API

| Area | Members |
|---|---|
| **Finding** | `ByTestId`, `Find`, `Wait`, `WaitOrNull`, `Exists`, `VisibleTestIds`, `SafePageSource` |
| **Readiness** | `AppState`, `CurrentRoute`, `CurrentUrl`, `RootAttribute`, `WaitForAppReady`, `WaitForRoute`, `WaitUntil` |
| **Interacting** | `Tap`, `TapIfPresent`, `Fill`, `TextOf`, `IntOf`, `IsEnabled`, `WaitUntilText`, `ScrollIntoView`, `Navigate` |
| **Asserting** | `Expect(testId)`, `ExpectCount(css)`, `ExpectCountWithPrefix(prefix)` — see [Assertions](#assertions) |
| **Reading** | `Count`, `CountTestIds`, `ColorOf`, `PropertyOf`, `AttributeOf` |
| **Diagnostics** | `TryCaptureScreenshot`, `Driver` |

---

## Assertions

`UiDriver` and `WebUi` both offer `Expect(id)` and `ExpectCount(...)`. They are the harness's answer
to "assert a colour, a text, a list length, a property of the app" — and they stay free of any test
framework: a failed expectation throws `HarnessAssertionException`, a plain `Exception` that every
runner reports as a failed test.

```csharp
Ui.Expect(AutomationIds.ProfileTotalXp)
    .IsVisible()
    .MatchesText(@"^\d+$")
    .HasTextColor("#7EB4F0")
    .HasProperty("Value", 120);          // a view-model value the app published

Ui.Expect(AutomationIds.ProfileEdit).Not.IsVisible();
Ui.ExpectCount("search-result-.*", scroll: true).AtLeast(3);

web.Expect(WebTestIds.ProfileTotalXp).HasTextColor("rgb(255, 255, 255)");
web.ExpectCountWithPrefix("web-lesson-").Is(12);
```

**They wait.** A UI settles asynchronously, so each check polls until it holds or the timeout
(`DefaultTimeout`, or `.WithTimeout(...)` per check) passes. A single read-and-compare is a race.
On failure the message says what it expected, what it last saw, how many attempts it made, what was
on screen, and where the screenshot went:

```
Expected 'profile-total-xp' to have TextColor #FF7EB4F0, but it was #FFFFFFFF (read as '#FFFFFFFF').
Gave up after 15s (37 attempts, timeout 15s).
Visible ids: page-profile, profile-name, profile-total-xp, ...
Screenshot: $TMPDIR/appium-harness-failures/expect-failed-'profile-total-xp'.png
```

### What can be asserted

| Check | Notes |
|---|---|
| `IsVisible`, `IsEnabled`, `IsToggledOn` | `Not.IsVisible()` waits for an element to go away; absent counts as not visible |
| `HasText`, `ContainsText`, `MatchesText` | exact and ordinal by default; opt in to `StringComparison` or `trim: true` |
| `HasTextColor`, `HasBackgroundColor`, `HasColor(name, …)` | compared as `HarnessColor`, so `#7EB4F0`, `#FF7EB4F0` and `rgb(126, 180, 240)` are the same colour. `tolerance:` absorbs 8-bit rounding |
| `HasAttribute(name, …)` | the raw platform attribute — `content-desc`, `value`, a DOM attribute |
| `HasProperty(name, …)` | a value the app published: string, `int`, or a predicate with a description |
| `ExpectCount(…).Is / AtLeast / AtMost / Between / IsEmpty` | the number of elements matching an id pattern (native) or selector (web) |

`Not` inverts **the next check only** — `Expect(id).Not.IsEnabled().Not.IsToggledOn()` reads as written.
An element that is absent, or a colour or property the app never published, satisfies neither
polarity of a value check (there is nothing to assert about it), so `Not.HasText(...)` on a missing
element and `Not.HasTextColor(...)` on an unpublished colour fail rather than passing for the wrong
reason. A typo'd property name, or a build with `TestProbe` compiled out, is therefore loud.

### Where colours and properties come from

The two layers get them differently, and that is the one thing worth understanding:

- **Web** — a DOM *has* them. Colours are the **computed** style (`getComputedStyle`: what the user
  sees after the stylesheet and inheritance), and properties are `data-*` attributes or DOM properties.
  Nothing to set up.
- **Native** — an accessibility tree carries text, ids and state flags, and **not** a MAUI `Color` or
  anything from a view model. The app must publish what a test wants to read. In the sample app that is
  [`TestProbe`](https://github.com/pawel51/AlohaAI/blob/main/src/AlohaAI/Infrastructure/TestProbe.cs), an attached property pair that folds the
  chosen values into a `[[probe:Name=value;…]]` payload (`ProbePayload`) in the platform's
  accessibility field — Android `contentDescription`, iOS `accessibilityValue` — and the harness reads
  it back.

```xml
<Label AutomationId="profile-total-xp" Text="{Binding TotalXp}" TextColor="#7EB4F0"
       probe:TestProbe.Properties="TextColor"
       probe:TestProbe.Value="{Binding TotalXp}" />
```

The payload format lives in one file, [`Probing/ProbePayload.cs`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Probing/ProbePayload.cs), which the app **link-compiles** rather than
referencing the harness — the app must not take `Appium.WebDriver` as a dependency, and sharing the
source is what stops the writer and the reader drifting apart. An app that adopts this copies the
idea, not the repo's wiring: publish whatever your tree can carry, and implement `PropertyOf` over it.

### Test builds only

The sample app's `TestProbe` compiles to nothing unless `DEBUG` or `ALOHA_WEB_INSPECTABLE` is defined — the same gate
as the inspectable WebView, for the same reason: a shipping build must expose no test surface. The
attached properties still exist in a shipping build so the XAML compiles; they publish nothing. A
colour or `HasProperty` check against a Release build therefore fails with `<not published>`, which
is the correct answer.

Do not put a probe on a control whose iOS accessibility *value* a test already reads (`Switch`,
`Entry`): that is the field it uses there.

XCUITest reports an element's text as its accessibility value when one is set, so on iOS a probed
label's raw `element.Text` is the payload, not the text. `UiDriver.TextOf`, `WaitUntilText` and
`Expect(...).HasText` strip the payload and fall back to the `label` attribute, so they read the
real text on both platforms. Code that reads `element.Text` directly bypasses that - go through
`TextOf`.

### Counting a list

`ExpectCount` counts what is **in the accessibility tree**, and both platforms virtualise: a list
longer than the screen reports only the rows currently rendered. Pass `scroll: true` to rewind to the
top, walk to the end and count the *distinct ids* seen. That needs one id per row (an id builder such
as `$"row-{item.Id}"` gives one), and it leaves the list scrolled to its end.

Without `scroll`, every matching *element* is counted; with it, every distinct *id*. They differ only
when ids repeat (a container and its child sharing one). If the list is still changing after
`maxScrolls` swipes (default 15 per direction) the count throws `InvalidOperationException` rather
than return a number it cannot vouch for: raise `maxScrolls`, or look for content that never settles.

---

## Screenshots

**When one is taken**

| Trigger | Taken? |
|---|---|
| An `Expect(...)` / `ExpectCount(...)` check fails | **Yes, automatically**, just before `HarnessAssertionException` is thrown |
| You call `ui.TryCaptureScreenshot(directory, name)` or `web.TryCaptureScreenshot(directory, name)` | Yes, when you call it |
| A test fails some other way (`Assert.That`, a `TimeoutException` from `Wait`) | **Not by the harness.** The sample app's E2E `BaseTest.[TearDown]` takes one for any failed test, see below |
| `PercyVisual.Snapshot` with `ALOHA_PERCY=1` | A Percy snapshot, which is a different thing: see below |

**Where it is saved: on the machine running the tests, never on the device.** The screenshot is
rendered on the device, returned to the test process over the Appium connection as PNG bytes, and
written to disk by the test process. Nothing is written to the device's storage. This is the same
for a local emulator or simulator and for a cloud device.

- **Failed expectations** go to `AppiumHarnessOptions.FailureScreenshotDirectory` (`WebUiOptions` has
  the same property). The e2e heads set it to `<head>/TestResults/Screenshots/`. If it is left null the
  default is `<temp>/appium-harness-failures/`, where `<temp>` is .NET's `Path.GetTempPath()`:

  | OS | `<temp>` |
  |---|---|
  | macOS | `$TMPDIR`, e.g. `/var/folders/q4/…/T/` |
  | Linux | `/tmp` |
  | Windows | `%TEMP%`, e.g. `C:\Users\you\AppData\Local\Temp\` |

  `<subject>` is the element id in quotes (or the id pattern, for a count), so one
  `Expect("profile-total-xp")` failure is `expect-failed-'profile-total-xp'.png`. **The same name
  overwrites**, so a re-run replaces the previous failure's image rather than adding to it. The
  exact path is printed in the failure message (`Screenshot: …`), which is the place to copy it from.

- **Any failed e2e test** (`Assert.That`, `Assert.Multiple`, a timeout) gets a screenshot from
  `BaseTest.[TearDown]`, saved as `<head>/TestResults/Screenshots/yyyyMMdd-HHmmss_<fixture.test(args)>.png`
  and attached to the NUnit result. The folder is git-ignored. See `FailureScreenshots.cs`.

- **`TryCaptureScreenshot(directory, name)`** writes `<directory>/<name>.png` where *you* choose
  `directory`. Both `UiDriver` and `WebUi` replace characters that are invalid in a file name with `_`. It returns the saved path, or `null` if the capture
  failed (and never throws, so diagnostics cannot add a failure of their own).

- **Percy snapshots** (`ALOHA_PERCY=1`, BrowserStack runs only) are not files on disk. They are
  captured by Percy and uploaded to its cloud for visual review.

**Keep it on CI.** The temp folder is not an artifact, and a CI runner discards it. Publish
`TestResults/Screenshots/` from the e2e head as a build artifact, or set `FailureScreenshotDirectory`
yourself. The folder may also hold app
content that was on screen (names, tokens), so give it the same retention as your logs.

**In a webview.** `web.Expect(...)` captures through the same Appium session and saves to the same
folder. Which pixels it contains (the web content only, or the whole screen) is decided by the
driver for the active context, so check one before relying on it for layout evidence.

---

## Options

[`AppiumHarnessOptions`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Session/AppiumHarnessOptions.cs) — platform, device,
server URI, timeouts, `AppId`, `AdditionalCapabilities`, `FailureScreenshotDirectory`, and a `Log`
hook you can point at your test framework's output.

[`WebUiOptions`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Web/WebUiOptions.cs) — `BaseUrl` (used to resolve `Navigate`
and to pick the right window), `DefaultTimeout`, `ShortTimeout`, `PollInterval`, `Log`,
`FailureScreenshotDirectory`, and the page contract: `TestIdAttribute` (`data-testid`),
`AppStateAttribute` (`data-app-state`), `ReadyState` (`ready`), `RouteAttribute` (`data-route`).

### Local vs cloud

The one thing to understand before using it. Against a cloud grid the vendor's SDK injects the
app, device and hub URI into the capabilities at run time, so **your code must set none of them**.
Locally nothing does that, so they come from the environment:

| Variable | Meaning |
|---|---|
| `APPIUM_APP` | path to the `.apk` / `.app` — **its presence switches on local mode** |
| `APPIUM_DEVICE` | device or simulator name |
| `APPIUM_PLATFORM_VERSION` | OS version (mostly for iOS simulators) |
| `APPIUM_SERVER` | Appium base URI (default `http://127.0.0.1:4723/`) |

`AppiumHarnessOptions.FromEnvironment(...)` reads them, and `IsLocal` is simply
"`AppPath` is set". With none set you get cloud-mode options — exactly what a grid SDK expects to
decorate.

Prefer an **absolute** `APPIUM_APP`: a relative path resolves against the test host's working
directory, which for `dotnet test` is the build output, not the project.
`AppiumSessionFactory` checks the path up front and fails naming the directory it resolved
against, because Appium's own error arrives only after session negotiation and says neither.

---

## Why it takes no test-framework dependency

The harness is a **wrapper, not a base class**. It references only `Appium.WebDriver` — no NUnit,
xUnit or MSTest — so the same package works from any of them, and from none.

That is a deliberate correction. Its predecessor was an xUnit base class, which meant the whole
suite inherited a hierarchy, tests could not be written in another framework, and moving to NUnit
meant rewriting the harness rather than reusing it. Compose it instead:

```csharp
var ui = AppiumSessionFactory.Start(
    AppiumHarnessOptions.FromEnvironment(
        MobilePlatform.Android, "Android Emulator", appId: "com.example.app"));

ui.Tap("login-button");
ui.Fill("username", "kai");
ui.HideKeyboard();
Assert.That(ui.Exists("home-page"), Is.True);   // or: ui.Expect("home-page").IsVisible();
```

`AppiumSessionFactory.CreateDriver` is public for the same reason: take the raw `AppiumDriver` and
wrap it yourself if a vendor SDK or a custom capability set built it.

---

## Install

```sh
dotnet add package Appium.Harness
```

Targets `net8.0` and `net10.0`. Proven against Appium **3.6.0**,
[uiautomator2](https://github.com/appium/appium-uiautomator2-driver) **8.2.2**,
[xcuitest](https://github.com/appium/appium-xcuitest-driver) **12.2.0**, `Appium.WebDriver`
**5.0.0**.

---

## Risks

| Risk | What goes wrong | How this library limits it |
|---|---|---|
| `Log` hook wired to CI output | Session diagnostics include element text, so app content — names, tokens rendered on screen — can land in a public build log | Opt-in and off by default; point it at test output that follows your repo's retention rules, not at stdout of a public job |
| `AdditionalCapabilities` | An unchecked passthrough to Appium. Capabilities can make the server download and run executables or relax device security | Nothing is added implicitly; you write every entry. Review them like code, and see the platform guides for the ones this repo sets |
| `Back()` on iOS | Reports success and does nothing, so an unwind loop can spin believing it is progressing and fail later somewhere unrelated | Documented above; leave pushed pages through the app's own back control |
| Asserting once instead of waiting | A single read-and-compare races the render, so a test passes or fails on machine speed | Use `Expect` / `ExpectCount`, which poll until the condition holds; keep timeouts bounded |
| A probe in a shipping build | A published payload exposes colours and view-model values to anything that reads the accessibility tree | Gate the app's probe behind a test-only build constant, as the sample app does (`DEBUG` or `ALOHA_WEB_INSPECTABLE`); never ship a build with it on |
| Counting a long list without `scroll: true` | Only rendered rows are in the tree, so the count stops at about a screenful and a real regression can hide behind it | Pass `scroll: true`, with a row id per item |
| `RestartApp()` | A process restart, **not** a data wipe — state in a server or a shared database survives it | Reset that state explicitly; do not treat a restart as isolation |

---

## Source layout

The library is one assembly, one namespace (`Appium.Harness`), with folders by concern:

| Folder | Concern | Public types |
|---|---|---|
| [`Session/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Session) | Opening a session: what to ask Appium for, local vs cloud | `AppiumHarnessOptions`, `AppiumSessionFactory`, `MobilePlatform` |
| [`Native/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Native) | The native tree: locating, interacting, gestures, reading probes | `UiDriver`, `SwipeDirection` |
| [`Web/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Web) | The DOM, and crossing into it from native | `WebUi`, `WebUiOptions`, `WebViewContext` |
| [`Expectations/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Expectations) | Waiting assertions and their failure messages, over a platform-neutral element probe | `ElementExpectation`, `CountExpectation`, `HarnessAssertionException` |
| [`Probing/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Probing) | Values an app publishes for tests, and comparing colours | `ProbePayload`, `ProbeProperties`, `HarnessColor` |
| [`Waiting/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Waiting) | The one polling primitive everything waits through | — |
| [`Diagnostics/`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Diagnostics) | Failure evidence (screenshots) | — |

Platform branching is confined to `Native/NativeLocators`, `NativeState` and `NativeGestures`;
everything above them is written once. The internal seams (`INativeScreen`, `IElementProbe`) are
what let the 300-odd unit tests run without a device.

---

## Building from source

```sh
dotnet build Appium.Harness.sln
dotnet test  Appium.Harness.sln
```

No device, emulator or Appium server is needed: the tests run against faked drivers. Device-level
coverage lives with the app that uses the harness — the sample app's
[E2E suite](https://github.com/pawel51/AlohaAI/blob/main/tests/AlohaAI.Tests.E2E).

### Packing and publishing

```sh
dotnet pack src/Appium.Harness -c Release -o artifacts
dotnet nuget push "artifacts/Appium.Harness.*.nupkg" --source https://api.nuget.org/v3/index.json --api-key <key>
```

The `.snupkg` symbol package is produced alongside and pushed with it. Bump `<Version>` in
[`Appium.Harness.csproj`](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness/Appium.Harness.csproj) and add an entry to
[`CHANGELOG.md`](https://github.com/pawel51/Appium.Harness/blob/main/CHANGELOG.md) first. Package metadata shared by every project (authors, licence,
repository, Source Link) is in [`Directory.Build.props`](https://github.com/pawel51/Appium.Harness/blob/main/Directory.Build.props); set `CI=true` on a
build server so paths in the PDB are normalised.

---

## License

[MIT](https://github.com/pawel51/Appium.Harness/blob/main/LICENSE).
