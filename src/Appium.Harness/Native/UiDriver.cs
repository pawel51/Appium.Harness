using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Appium.Harness;

/// <summary>
/// The interaction surface: find, wait, tap, type, read, scroll, swipe.
///
/// Deliberately a <em>wrapper</em> rather than a base class. Inheriting from a harness forces
/// every test into one hierarchy and couples it to whichever test framework the base class
/// used. Compose it instead: hold a <see cref="UiDriver"/>, or wrap it in your own app-specific
/// facade.
///
/// Element lookup is by <b>automation id</b> throughout, which is the one identifier that
/// survives both platforms: MAUI maps <c>AutomationId</c> to the Android <c>resource-id</c>
/// and the iOS accessibility id. <see cref="ByAutomationId"/> picks the right strategy.
///
/// <para>The type is split by concern across partial files: this one locates and interacts,
/// <c>UiDriver.Gestures.cs</c> scrolls and swipes, <c>UiDriver.Assertions.cs</c> reads and
/// expects, and <c>UiDriver.WebView.cs</c> crosses into an embedded WebView. The platform
/// branching itself lives in <see cref="NativeLocators"/>, <see cref="NativeState"/> and
/// <see cref="NativeGestures"/>.</para>
/// </summary>
public sealed partial class UiDriver : IDisposable
{
    private readonly AppiumHarnessOptions _options;

    /// <summary>Wraps an existing driver — e.g. one a cloud vendor's SDK created.</summary>
    public UiDriver(AppiumDriver driver, AppiumHarnessOptions options)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _gestures = new NativeGestures(driver, IsAndroid);
    }

    /// <summary>The underlying driver, for anything this wrapper does not cover.</summary>
    public AppiumDriver Driver { get; }

    /// <summary>True when driving Android.</summary>
    public bool IsAndroid => _options.Platform == MobilePlatform.Android;

    /// <summary>True when driving iOS.</summary>
    public bool IsIOS => !IsAndroid;

    private TimeSpan DefaultTimeout => _options.DefaultTimeout;

    #region Locating

    /// <summary>
    /// The locator for an automation id on the current platform: Android <c>resource-id</c>,
    /// iOS accessibility id. Using anything else (XPath, text) is what makes UI tests brittle.
    /// </summary>
    public By ByAutomationId(string automationId) => NativeLocators.ById(IsAndroid, automationId);

    /// <summary>Finds an element now, throwing if it is absent. Prefer <see cref="Wait"/>.</summary>
    public AppiumElement Find(string automationId) =>
        (AppiumElement)Driver.FindElement(ByAutomationId(automationId));

    /// <summary>
    /// Waits until the element is present <b>and displayed</b>, then returns it.
    /// Throws <see cref="TimeoutException"/> with the current screen's id list attached — a
    /// bare "no such element" tells you nothing about where the app actually was.
    /// </summary>
    public AppiumElement Wait(string automationId, TimeSpan? timeout = null) =>
        WaitOrNull(automationId, timeout)
        ?? throw new TimeoutException(
            $"Timed out after {(timeout ?? DefaultTimeout).TotalSeconds:0.#}s waiting for '{automationId}'. " +
            VisibleIdsDiagnostic());

    /// <summary>
    /// Waits for the element and returns <see langword="null"/> on timeout instead of throwing —
    /// for branching on whether something rendered.
    /// </summary>
    public AppiumElement? WaitOrNull(string automationId, TimeSpan? timeout = null) =>
        Poll(() => Find(automationId) is { Displayed: true } element ? element : null, timeout);

    /// <summary>
    /// Waits for the element to exist in the tree even if it is not displayed — for content
    /// below the fold, which reports <c>Displayed == false</c> until it is scrolled into view.
    /// </summary>
    public AppiumElement? WaitPresent(string automationId, TimeSpan? timeout = null) =>
        Poll(() => Find(automationId), timeout);

    /// <summary>Whether the element is currently displayed. Never throws.</summary>
    public bool Exists(string automationId, TimeSpan? timeout = null)
    {
        if (timeout is not null)
        {
            return WaitOrNull(automationId, timeout) is not null;
        }

        try
        {
            return Find(automationId).Displayed;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether any element's automation id matches the regex.
    ///
    /// The pattern is matched against the <b>simple</b> id — the value you set as
    /// <c>AutomationId</c> — on both platforms, so <c>"search-result-.*"</c> means what it looks
    /// like it means (see <see cref="NativeLocators.ByIdRegex"/> for what that takes on Android).
    /// </summary>
    public bool AnyIdMatches(string idRegex) => FindByIdRegex(idRegex).Count > 0;

    /// <summary>Elements whose simple id matches the whole regex — behind <see cref="AnyIdMatches"/> and <see cref="CountIds"/>.</summary>
    private IReadOnlyList<IWebElement> FindByIdRegex(string idRegex) =>
        Driver.FindElements(NativeLocators.ByIdRegex(IsAndroid, idRegex));

    /// <summary>Automation ids of everything currently on screen. The core failure diagnostic.</summary>
    public IReadOnlyList<string> VisibleAutomationIds()
    {
        try
        {
            var attribute = NativeLocators.IdAttribute(IsAndroid);
            return Driver.FindElements(NativeLocators.Everything)
                .Select(e =>
                {
                    try { return e.GetAttribute(attribute); }
                    catch (WebDriverException) { return null; }
                })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => NativeLocators.SimpleId(id!))
                .Distinct()
                .ToList();
        }
        catch (WebDriverException)
        {
            return [];
        }
    }

    /// <summary>Page source, or empty if the driver cannot produce it. Never throws.</summary>
    public string SafePageSource()
    {
        try
        {
            return Driver.PageSource ?? string.Empty;
        }
        catch (WebDriverException)
        {
            return string.Empty;
        }
    }

    /// <summary>The line every native failure message ends with: what was actually on screen.</summary>
    internal string VisibleIdsDiagnostic() =>
        $"Visible ids: {string.Join(", ", VisibleAutomationIds().Take(40))}";

    #endregion

    #region Interacting

    /// <summary>Waits for the element, then taps it.</summary>
    public void Tap(string automationId, TimeSpan? timeout = null) => Wait(automationId, timeout).Click();

    /// <summary>Taps if present; returns whether it was. For optional UI such as a dismissible banner.</summary>
    public bool TapIfPresent(string automationId, TimeSpan? timeout = null)
    {
        var element = WaitOrNull(automationId, timeout ?? TimeSpan.FromSeconds(2));
        if (element is null)
        {
            return false;
        }

        element.Click();
        return true;
    }

    /// <summary>Clears the field and types into it. Does not dismiss the keyboard — see <see cref="HideKeyboard"/>.</summary>
    public void Fill(string automationId, string text)
    {
        var element = Wait(automationId);
        element.Clear();
        element.SendKeys(text);
    }

    /// <summary>Visible text of an element.</summary>
    public string TextOf(string automationId) => NativeText.Of(Wait(automationId));

    /// <summary>
    /// Whether a control is enabled.
    ///
    /// Worth reaching for whenever a control signals "done" by greying out rather than
    /// disappearing: <see cref="Exists"/> stays true for a disabled control on both platforms
    /// (Android's <c>isShown()</c> and XCUITest's <c>displayed</c> are both independent of
    /// enabled-ness), so a presence check cannot tell the two states apart.
    /// </summary>
    public bool IsEnabled(string automationId) => NativeState.IsEnabled(Wait(automationId));

    /// <summary>
    /// Whether a switch/toggle is on. The attribute differs by platform — Android exposes
    /// <c>checked</c>, iOS <c>value</c> — which is a classic source of a test that passes on
    /// one platform and silently inverts on the other.
    /// </summary>
    public bool IsToggledOn(string automationId) => NativeState.IsToggledOn(Wait(automationId), IsAndroid);

    /// <summary>Waits until an element's text equals <paramref name="expected"/>.</summary>
    public bool WaitUntilText(string automationId, string expected, TimeSpan? timeout = null) =>
        Poll(() => NativeText.Of(Find(automationId)) == expected ? expected : null, timeout) is not null;

    /// <summary>
    /// Finds an element by its visible text. Needed for platform chrome that carries no
    /// automation id — tab bars, system dialog buttons. Prefer ids for anything you own.
    /// </summary>
    public AppiumElement? WaitByText(string text, TimeSpan? timeout = null)
    {
        var by = NativeLocators.ByText(IsAndroid, text);
        return Poll(() => Driver.FindElements(by).FirstOrDefault() as AppiumElement, timeout);
    }

    /// <summary>Taps an element by its visible text.</summary>
    public void TapByText(string text, TimeSpan? timeout = null)
    {
        var element = WaitByText(text, timeout)
            ?? throw new TimeoutException($"No element with text '{text}'. {VisibleIdsDiagnostic()}");
        element.Click();
    }

    /// <summary>Accepts a native alert. Returns whether one was there.</summary>
    public bool AcceptAlert(string buttonText = "OK")
    {
        // Tap the button we were actually asked for, before considering Alert().Accept().
        //
        // "Accept" is platform-defined, and the platforms disagree: Android presses the
        // positive button, iOS presses the last one. For a two-button alert whose buttons are
        // "Reset" and "Cancel", that means iOS cancels. The alert closes either way, so the
        // call reports success, the test carries on, and the action it asked for simply never
        // happened.
        var button = WaitByText(buttonText, TimeSpan.FromSeconds(3));
        if (button is not null)
        {
            button.Click();
            return true;
        }

        try
        {
            // No button with that label: either MAUI rendered a genuinely native alert whose
            // buttons are not in the element tree, or the alert is already gone.
            Driver.SwitchTo().Alert().Accept();
            return true;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// Closes the soft keyboard. On iOS it overlays the lower screen and will silently eat
    /// taps on whatever it covers, so type-then-dismiss should be reflexive.
    /// </summary>
    public void HideKeyboard()
    {
        try
        {
            Driver.HideKeyboard();
            return;
        }
        catch (WebDriverException)
        {
            // XCUITest frequently cannot dismiss on its own; tap a return key instead.
        }

        foreach (var key in new[] { "Done", "done", "return", "Return" })
        {
            try
            {
                Driver.FindElement(NativeLocators.ByText(IsAndroid, key)).Click();
                return;
            }
            catch (WebDriverException)
            {
                // try the next label
            }
        }
    }

    /// <summary>Navigates back; returns false if there was nowhere to go (iOS throws in that case).</summary>
    public bool Back()
    {
        try
        {
            Driver.Navigate().Back();
            return true;
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// Terminates and re-activates the app — a process restart, not a data wipe. Requires
    /// <see cref="AppiumHarnessOptions.AppId"/>.
    /// </summary>
    public void RestartApp()
    {
        if (string.IsNullOrWhiteSpace(_options.AppId))
        {
            throw new InvalidOperationException(
                $"{nameof(RestartApp)} needs {nameof(AppiumHarnessOptions)}.{nameof(AppiumHarnessOptions.AppId)}.");
        }

        try { Driver.TerminateApp(_options.AppId); } catch (WebDriverException) { /* not running */ }
        try { Driver.ActivateApp(_options.AppId); } catch (WebDriverException) { /* grid hiccup */ }
    }

    #endregion

    #region Session

    /// <summary>Captures a PNG screenshot, returning its path, or null if capture failed.</summary>
    public string? TryCaptureScreenshot(string directory, string name) =>
        Screenshots.TryCapture(Driver, directory, name, _options.Log, "[harness]");

    /// <summary>Ends the session. Safe to call twice.</summary>
    public void Dispose()
    {
        try { Driver.Quit(); } catch (WebDriverException) { /* already gone */ }
        Driver.Dispose();
    }

    #endregion

    /// <summary>
    /// Polls until <paramref name="read"/> yields an element, on the configured interval. A
    /// <see cref="WebDriverException"/> — not in the tree yet, tree mid-update — means "not yet".
    /// </summary>
    private T? Poll<T>(Func<T?> read, TimeSpan? timeout) where T : class =>
        Retry.Poll(read, timeout ?? DefaultTimeout, _options.PollInterval);
}
