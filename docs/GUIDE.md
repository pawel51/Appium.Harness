# Getting started: from an empty project to a passing E2E test

A complete, ordered walkthrough for adding Appium UI tests to a .NET MAUI app using
`Appium.Harness`. It assumes **no prior Appium knowledge** and nothing about this repository.

By the end you will have a test that installs your app on a device, drives its UI, and asserts
something real — plus enough understanding to debug it when it breaks.

**This guide is self-contained.** Platform setup (emulators, simulators, build flags) and
WebView support live in their own guides, linked once at [the end](#11-next-steps). You do not
need them to finish this one.

| | |
|---|---|
| **Time** | ~45 minutes, most of it waiting for builds |
| **You need** | a macOS, Windows or Linux machine and a .NET MAUI app you can build |

**Contents** — [1. What you are building](#1-what-you-are-building) ·
[2. Concepts](#2-concepts-worth-five-minutes) · [3. Prerequisites](#3-prerequisites) ·
[4. Test project](#4-create-the-test-project) · [5. Make the app testable](#5-make-the-app-testable) ·
[6. Session setup](#6-write-the-session-setup) · [7. Lifecycle](#7-write-the-test-lifecycle) ·
[8. Page object](#8-write-a-page-object) · [9. First test](#9-write-and-run-the-first-test) ·
[10. When it fails](#10-when-it-fails) · [11. Next steps](#11-next-steps) · [Risks](#risks)

---

## 1. What you are building

Four processes. Nothing is mocked — a real app runs on a real device or emulator and something
genuinely taps its screen.

```
┌── your machine ────────────────────────────────────────────────────┐
│                                                                    │
│   dotnet test                 Appium server                        │
│   (the test host)             :4723                                │
│        │                          │                                │
│        │  W3C WebDriver over HTTP │                                │
│        └─────────────────────────►│                                │
│                                   │  UIAutomator2 (Android)        │
│                                   │  XCUITest (iOS)                │
│                                   ▼                                │
│                      ┌── device / emulator ──┐                     │
│                      │   your app            │                     │
│                      └───────────────────────┘                     │
└────────────────────────────────────────────────────────────────────┘
```

| Piece | Responsible for |
|---|---|
| **Test host** | your NUnit tests and page objects; speaks WebDriver over HTTP |
| **Appium server** | translating WebDriver calls into platform automation; installing and launching the app |
| **Platform driver** | UIAutomator2 or XCUITest, doing the actual tapping and tree reading |
| **Device** | running your app, exactly as a user would |

`Appium.Harness` sits inside the test host: it is the layer between your test's intent
(*"tap Login"*) and the WebDriver call that achieves it.

---

## 2. Concepts worth five minutes

Read these now and the rest of the guide will not surprise you.

### The three types

`UiDriver` drives the **native** UI by automation id. `WebUi` drives an embedded **WebView**'s DOM
by `data-testid`. `WebViewContext` is the scope that switches a session between them. A purely
native app only ever needs `UiDriver`.

Full explanation in **[README.md](https://github.com/pawel51/Appium.Harness/blob/main/README.md#the-three-types-and-why-there-are-three)** — it is the
reference for this guide.

### Automation ids are the contract

Tests find elements by **id**, never by position or visible text. Text changes with copy edits and
localisation; position changes with layout. An id changes only when someone means it to.

### One session per test

Each test opens its own Appium session and closes it afterwards. That is slower than sharing one
— and correct. A shared session carries state between tests, so failures depend on ordering and a
crash in test 3 takes out tests 4 onward. It also matters for cloud grids, whose SDKs bind a
session to whichever test created it. ⚠ [Risks](#risks)

### Local and cloud modes

The same code runs against a local emulator or a cloud device grid. Locally you supply the app and
device through environment variables; on a grid the vendor's SDK injects them and **your code must
set neither**. `AppiumHarnessOptions.FromEnvironment` handles both: `APPIUM_APP` being set is what
means "local".

This guide only covers local runs.

---

## 3. Prerequisites

Install these in order. Each step ends with a check — run it, because a missing piece here
surfaces much later as a confusing failure.

1. **Node.js 20 or newer.** Appium is a Node application.

   ```sh
   node --version        # → v20.x or newer
   ```

2. **Appium 3.**

   ```sh
   npm install -g appium
   appium --version      # → 3.6.0 or newer
   ```

3. **A platform driver.** Install the one(s) you need:

   ```sh
   appium driver install uiautomator2     # Android
   appium driver install xcuitest         # iOS (macOS only)

   appium driver list --installed
   # → uiautomator2@8.2.2 [installed (npm)]
   # → xcuitest@12.2.0 [installed (npm)]
   ```

4. **The .NET SDK** matching your app (this repo uses .NET 10).

   ```sh
   dotnet --version
   ```

5. **The platform toolchain** — Android SDK, or Xcode for iOS. Full setup, including creating an
   emulator or simulator, is in the platform guides linked at [the end](#11-next-steps). For now
   it is enough that you can build and run your app by hand.

6. **Confirm the server starts.** In a second terminal:

   ```sh
   appium
   ```

   Then from the first:

   ```sh
   curl -s http://127.0.0.1:4723/status
   # → {"value":{"ready":true,"message":"The server is ready to accept new connections",...
   ```

   Leave it running; every test run needs it.

---

## 4. Create the test project

A test project per platform. They contain almost nothing: the tests themselves are shared source
compiled into both.

1. **Create the projects.**

   ```sh
   dotnet new nunit -o tests/MyApp.Tests.E2E/Android
   dotnet new nunit -o tests/MyApp.Tests.E2E/iOS
   mkdir tests/MyApp.Tests.E2E/Shared
   ```

2. **Add the packages** to each:

   ```sh
   dotnet add tests/MyApp.Tests.E2E/Android package Appium.WebDriver
   dotnet add tests/MyApp.Tests.E2E/Android package Appium.Harness
   ```

   NUnit, `NUnit3TestAdapter` and `Microsoft.NET.Test.Sdk` come from the template.

3. **Link-compile the shared source.** In *each* platform `.csproj`:

   ```xml
   <ItemGroup>
     <Compile Include="..\Shared\**\*.cs" LinkBase="Shared" Visible="true"
              Exclude="..\Shared\obj\**;..\Shared\bin\**" />
   </ItemGroup>
   ```

   **Why link-compiled and not a referenced library** — the question everyone asks:

   - **vstest discovers tests in the assembly it runs, not in the ones that assembly
     references.** Tests in a separate library would simply never be found.
   - The shared source **cannot compile on its own** anyway: it calls `AppiumSetup`, which each
     platform project defines differently (step 6).

   `Visible="true"` makes the files appear under a *Shared* node in your IDE. Without it they are
   invisible in the solution tree, because their folder has no project of its own.

4. **Verify** both projects build:

   ```sh
   dotnet build tests/MyApp.Tests.E2E/Android
   ```

---

## 5. Make the app testable

Tests can only find what the app exposes.

1. **Put an `AutomationId` on every element a test will touch** — and on one root element per
   page, which is what lets a test say *where it is*:

   ```xml
   <Grid AutomationId="page-home">
       <Button AutomationId="home-login" Text="Log in" />
   </Grid>
   ```

2. **Keep the strings in one shared constants class**, in a project both the app and the tests
   reference:

   ```csharp
   public static class AutomationIds
   {
       public const string PageHome = "page-home";
       public const string HomeLogin = "home-login";

       // Builders for per-item ids, so a list row is addressable.
       public static string LessonRow(string moduleId, string lessonId) =>
           $"lesson-{moduleId}-{lessonId}";
   }
   ```

   Then reference it from XAML:

   ```xml
   xmlns:auto="clr-namespace:MyApp.Automation;assembly=MyApp.Core"
   ...
   <Grid AutomationId="{x:Static auto:AutomationIds.PageHome}">
   ```

   **Why bother:** a renamed id is now a **compile error on both sides** instead of a test that
   fails three minutes into a device run with "no such element".

3. **Pick a convention and keep it.** This repo uses lower-kebab-case, `page-*` for page roots,
   `<screen>-<thing>` elsewhere. Consistency is what makes a failure's id list readable.

4. **Verify** the app still builds and the ids reach the device — run the app and check with
   [Appium Inspector](https://github.com/appium/appium-inspector), or after your first test run
   read the id list any failure prints.

> **Enforce the conventions in a unit test** — one that reflects over the id class and checks
> every id is unique and well-formed is a one-second gate instead of a three-minute one.

---

## 6. Write the session setup

One `AppiumSetup` per platform project. This is the only file that differs between them.

1. **`tests/MyApp.Tests.E2E/Android/AppiumSetup.cs`:**

   ```csharp
   using Appium.Harness;

   namespace MyApp.Tests.E2E;

   public static class AppiumSetup
   {
       private const string AppPackage = "com.example.myapp";

       public static UiDriver CreateSession(Action<string>? log = null) =>
           AppiumSessionFactory.Start(AppiumHarnessOptions.FromEnvironment(
               MobilePlatform.Android,
               defaultDeviceName: "Android Emulator",
               appId: AppPackage,
               log: log));
   }
   ```

2. **`tests/MyApp.Tests.E2E/iOS/AppiumSetup.cs`** — identical but `MobilePlatform.iOS`, a
   simulator name such as `"iPhone 17"`, and the bundle id.

   Both classes have the **same name and namespace**. The shared test source calls
   `AppiumSetup.CreateSession(...)` and gets whichever one was compiled into the assembly being
   run. That is the whole platform-switching mechanism.

3. **Know the environment variables** — this is how a local run is configured:

   | Variable | Meaning |
   |---|---|
   | `APPIUM_APP` | absolute path to the `.apk` / `.app`. **Its presence is what switches on local mode** |
   | `APPIUM_DEVICE` | device or simulator name; overrides `defaultDeviceName` |
   | `APPIUM_PLATFORM_VERSION` | OS version (mostly iOS simulators) |
   | `APPIUM_SERVER` | Appium base URI, default `http://127.0.0.1:4723/` |

   Use an **absolute** `APPIUM_APP`. A relative path resolves against the test host's working
   directory — the build output, not your project — so a sensible-looking `../../../src/...`
   silently resolves inside `bin/`.

---

## 7. Write the test lifecycle

One base class in `Shared/`, holding the NUnit plumbing and nothing else.

1. **`Shared/BaseTest.cs`:**

   ```csharp
   using Appium.Harness;
   using NUnit.Framework;
   using NUnit.Framework.Interfaces;

   namespace MyApp.Tests.E2E;

   public abstract class BaseTest
   {
       private UiDriver? _ui;

       protected UiDriver Ui => _ui ?? throw new InvalidOperationException("No active session.");

       [SetUp]
       public void StartSession() => _ui = AppiumSetup.CreateSession(TestContext.Out.WriteLine);

       [TearDown]
       public void EndSession()
       {
           if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed && _ui is not null)
           {
               try
               {
                   TestContext.Out.WriteLine(
                       $"[diagnostics] visible ids: {string.Join(", ", _ui.VisibleAutomationIds().Take(40))}");
               }
               catch (Exception ex)
               {
                   TestContext.Out.WriteLine($"[diagnostics] unavailable: {ex.Message}");
               }
           }

           _ui?.Dispose();
           _ui = null;
       }
   }
   ```

2. **Do not skip the failure diagnostics.** They cost nothing on a passing run and are the
   difference between *"element not found"* and *"the app was still on the login screen"*. Wrap
   them in `try`/`catch`: diagnostics must never replace the real failure with one of their own.

3. **`[SetUp]`, not `[OneTimeSetUp]`.** See [Concepts](#2-concepts-worth-five-minutes).

4. **Reset shared state here too.** If your app reads from a server or a database that an app
   reinstall does not clear, clear it in `[SetUp]`. A fresh driver reinstalls the app; it does not
   empty a database. Skipping this produces failures that only appear in one test ordering — among
   the most expensive bugs to chase. ⚠ [Risks](#risks)

---

## 8. Write a page object

A facade that knows your app. Tests speak in app vocabulary; the page object owns the ids.

1. **`Shared/MyApp.cs`:**

   ```csharp
   public sealed class MyApp
   {
       private readonly UiDriver _ui;

       public MyApp(UiDriver ui) => _ui = ui;

       /// <summary>The page id currently on screen, for failure messages.</summary>
       public string CurrentPage =>
           _ui.VisibleAutomationIds().FirstOrDefault(id => id.StartsWith("page-"))
           ?? "(no page id on screen)";

       public void LogIn(string user, string password)
       {
           _ui.Fill(AutomationIds.LoginUser, user);
           _ui.Fill(AutomationIds.LoginPassword, password);
           _ui.HideKeyboard();          // iOS: the keyboard covers and eats taps
           _ui.Tap(AutomationIds.LoginSubmit);
           _ui.Wait(AutomationIds.PageHome);
       }
   }
   ```

2. **Rules that keep page objects useful:**

   - **A method ends when the app has settled** — wait for the destination, do not return mid-
     transition and leave the test to guess.
   - **No assertions inside it.** It performs and observes; tests decide what is correct.
   - **Scroll to anything that might be below the fold.** An off-screen control is reported as
     *absent*, not as off-screen — see [Risks](#risks).
   - **Never `Thread.Sleep`.** Wait for a condition. A sleep is either too short (flaky) or too
     long (slow), and usually both on different machines.

3. **Expose a `CurrentPage`-style property.** Every failure message gets better for free.

---

## 9. Write and run the first test

1. **`Shared/SmokeTests.cs`:**

   ```csharp
   using NUnit.Framework;

   namespace MyApp.Tests.E2E;

   [TestFixture]
   public class SmokeTests : BaseTest
   {
       [Test]
       public void AppLaunches_ReachesHome()
       {
           var app = new MyApp(Ui);

           Assert.That(Ui.WaitOrNull(AutomationIds.PageHome), Is.Not.Null,
               $"Home never rendered. Current page: {app.CurrentPage}");
       }
   }
   ```

   Start here, however trivial it looks. It catches a build that installs but crashes on launch,
   and an empty UI tree from a mis-configured automation engine — two failures that otherwise
   masquerade as every test being broken.

2. **Build the app artifact** and note its absolute path. Which artifact depends on the platform;
   see the platform guides.

3. **Start the device** (emulator or simulator) and the Appium server.

4. **Run:**

   ```sh
   cd tests/MyApp.Tests.E2E/Android
   export APPIUM_APP="/absolute/path/to/your.apk"
   export APPIUM_DEVICE="Android Emulator"
   dotnet test
   ```

5. **Expect** the app to install, launch, and the test to pass:

   ```
   Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1
   ```

6. **Run a single test** while iterating — a device run is slow enough that you want this:

   ```sh
   dotnet test --filter "Name=AppLaunches_ReachesHome"
   ```

### Assert what it looks like, not only that it exists

`Exists` answers "is it on screen". For text, colour, list lengths and values the app holds, use
`Expect` — it waits for the condition rather than sampling once, and its failure says what it saw:

```csharp
Ui.Expect(AutomationIds.ProfileName).HasText("Kai");
Ui.Expect(AutomationIds.ProfileTotalXp).HasTextColor("#7EB4F0").HasProperty("Value", 120);
Ui.ExpectCount("search-result-.*", scroll: true).AtLeast(3);
```

Text and counts work out of the box. **Colours and view-model values on a native screen need the app
to publish them** — an accessibility tree does not carry them — which is what `TestProbe` from the [Appium.Harness.Maui](https://github.com/pawel51/Appium.Harness/blob/main/src/Appium.Harness.Maui/README.md) package does; see
[Assertions](https://github.com/pawel51/Appium.Harness/blob/main/README.md#assertions) for the setup, the `scroll: true` caveat, and why it is test-build only.

---

## 10. When it fails

Work down this list. Most first-run failures are in the first three.

1. **Read the id list in the exception.** `Wait` prints the ids actually on screen. If they are
   the login screen's, your test is on the wrong page — not missing an element.

2. **Is it the app or the test?** Launch the app by hand and do the same steps. If it fails there
   too, you have an app bug, and no amount of Appium tuning will help.

3. **Check the app that is actually installed.** A test that "ignores your fix" is often running
   a stale build — see [Risks](#risks).

4. **Read the Appium server log.** It shows every command and, crucially, what it did *not* do.
   Lines like *"There is no need to install/upgrade"* are the kind that explain an otherwise
   impossible result.

5. **Capture more.** `Ui.SafePageSource()` dumps the tree; `Ui.TryCaptureScreenshot(dir, name)`
   saves a PNG. Both are safe in a `catch`.

6. **Inspect interactively** with [Appium Inspector](https://github.com/appium/appium-inspector)
   — it attaches to a running app and shows the tree Appium sees, which is not always the tree
   you expect.

### Failures that look like something else

| Symptom | Actually |
|---|---|
| "Element not found", but it is plainly on screen | Below the fold. Android's accessibility tree carries only *rendered* nodes, so an off-screen control is **absent**, not off-screen. Scroll to it |
| A test asserts a value that is right on screen but wrong in the assertion | The read happened mid-refresh. Wait for the value, not for the page |
| Passes alone, fails in the suite | Shared state that an app reinstall does not clear |
| Passes on Android, fails on iOS (or vice versa) | A platform-specific attribute or gesture. See the platform notes in [README.md](https://github.com/pawel51/Appium.Harness/blob/main/README.md#uidriver--the-native-layer) |

---

## 11. Next steps

Platform setup — SDKs, emulators, capabilities — is the drivers' own documentation:

- **[UiAutomator2 driver (Android)](https://github.com/appium/appium-uiautomator2-driver)**
- **[XCUITest driver (iOS)](https://appium.github.io/appium-xcuitest-driver/latest/)**

If your app embeds a WebView, see
[the README](https://github.com/pawel51/Appium.Harness/blob/main/README.md#webviewcontext--the-seam)
for what the app must do before a test can enter it.

Further reading: [Appium documentation](https://appium.io/docs/en/latest/) ·
[W3C WebDriver spec](https://www.w3.org/TR/webdriver/) ·
[MAUI accessibility and `AutomationId`](https://learn.microsoft.com/dotnet/maui/fundamentals/accessibility)

---

## Risks

| Risk | What goes wrong | How to limit it |
|---|---|---|
| **A stale app under test** | An app that is already installed may not be replaced by the one you point at — a dev build's version rarely changes, and that is what the drivers compare. The run silently tests the previous binary and reports on code that is not the code under test. **Both platforms**, and neither says so | Android has `enforceAppInstall`; iOS has no equivalent, so uninstall after rebuilding. When a result looks impossible, compare the installed binary with the one you built before believing it |
| **Sharing a driver across tests** | State leaks between tests, so failures depend on ordering; a crash takes out every later test; cloud SDKs leave later tests with no session at all | One session per test, as in step 7 |
| **Unreset shared state** | A reinstall clears the app, not your server or database. Test *n* inherits test *n-1*'s data and the symptom only appears in one ordering | Reset explicitly in `[SetUp]` |
| **Sleeps instead of waits** | Too short is flaky, too long is slow, and machine speed decides which you get | Wait on a condition. Use `WaitUntilText`, or poll for the value a screen is settling on |
| **Asserting a value the app is still writing** | A screen can show a pre-refresh figure and a post-refresh one at the same moment; a single read samples a race | Wait for the expected value with a bounded timeout, then assert |
| **Secrets in capabilities or test data** | Capabilities and failure diagnostics are printed to logs, and a device log is easy to share | Keep credentials in environment variables or a secret store; never commit them, and prefer dedicated test accounts |
| **Diagnostics that leak app content** | The `Log` hook and page-source dumps contain whatever is on screen | Point them at test output that respects your repo's retention rules, not at a public job's stdout |
