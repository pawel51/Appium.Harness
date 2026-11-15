using System.Globalization;
using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// The element layer for web content, mirroring <see cref="UiDriver"/>'s vocabulary so that
/// moving between a native page object and a web one is not a change of language.
///
/// <para><b>It takes <see cref="IWebDriver"/>, not <c>AppiumDriver</c>, and that single
/// signature decision is the point of the class.</b> <c>AppiumDriver</c> derives from
/// <c>WebDriver</c>, so once a session is switched into a <c>WEBVIEW_*</c> context the very
/// same driver answers CSS queries against the live DOM. The same page objects therefore drive
/// an embedded WebView on a device and, if anyone ever wants it, a desktop browser — without a
/// second implementation to keep in step.</para>
///
/// <para><b>Waits, never sleeps.</b> A SPA has no page loads, so there is nothing to wait on by
/// default — which is why the app is expected to publish its state (<c>data-app-state</c>) and
/// route (<c>data-route</c>) on its root element. Every navigation helper here waits on those.
/// The attribute names are <see cref="WebUiOptions"/> settings, for an app that spells them
/// differently.</para>
/// </summary>
public sealed partial class WebUi
{
    private readonly WebUiOptions _options;

    /// <param name="driver">A driver already pointed at web content — a browser driver, or an Appium session switched into a webview context.</param>
    /// <param name="options">Timeouts, polling and logging.</param>
    public WebUi(IWebDriver driver, WebUiOptions? options = null)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? new WebUiOptions();
    }

    /// <summary>The underlying driver, for anything this class does not cover.</summary>
    public IWebDriver Driver { get; }

    private IJavaScriptExecutor Script => (IJavaScriptExecutor)Driver;

    private void Log(string message) => _options.Log?.Invoke(message);

    // ---- Locating -------------------------------------------------------------------------

    /// <summary>The locator for a test id. One place, so the attribute name is never repeated.</summary>
    public By ByTestId(string testId) => By.CssSelector($"[{_options.TestIdAttribute}='{testId}']");

    /// <summary>Finds an element immediately, throwing if it is not there.</summary>
    public IWebElement Find(string testId) => Driver.FindElement(ByTestId(testId));

    /// <summary>
    /// Waits for a displayed element and returns it, or throws with the diagnostic that
    /// actually helps: what <i>was</i> on screen, and where the SPA thinks it is.
    /// </summary>
    public IWebElement Wait(string testId, TimeSpan? timeout = null)
    {
        var element = WaitOrNull(testId, timeout);
        if (element is not null)
            return element;

        throw new WebDriverTimeoutException(
            $"No visible element with {_options.TestIdAttribute}='{testId}' after {(timeout ?? _options.DefaultTimeout).TotalSeconds:0.#}s. " +
            Diagnostics());
    }

    /// <summary>Waits for a displayed element, returning null rather than throwing.</summary>
    public IWebElement? WaitOrNull(string testId, TimeSpan? timeout = null) =>
        Poll(() => Driver.FindElements(ByTestId(testId)).FirstOrDefault(e => e.Displayed), timeout);

    /// <summary>Whether a displayed element exists within the timeout.</summary>
    public bool Exists(string testId, TimeSpan? timeout = null) =>
        WaitOrNull(testId, timeout ?? _options.ShortTimeout) is not null;

    /// <summary>Every test id currently in the DOM. The web twin of <c>UiDriver.VisibleAutomationIds</c>.</summary>
    public IReadOnlyList<string> VisibleTestIds()
    {
        try
        {
            return
            [
                .. Driver.FindElements(By.CssSelector($"[{_options.TestIdAttribute}]"))
                    .Select(e => Safe(() => e.GetAttribute(_options.TestIdAttribute)))
                    .OfType<string>()
                    .Where(id => id.Length > 0)
            ];
        }
        catch (WebDriverException)
        {
            return [];
        }
    }

    /// <summary>Page source, or a note explaining why it could not be read. Never throws.</summary>
    public string SafePageSource() => Safe(() => Driver.PageSource) ?? "<page source unavailable>";

    // ---- The SPA readiness contract ---------------------------------------------------------

    /// <summary>The SPA's own state: booting, loading, ready or error. See <see cref="WebUiOptions.AppStateAttribute"/>.</summary>
    public string AppState => RootAttribute(_options.AppStateAttribute) ?? "unknown";

    /// <summary>The current route name, as the SPA's router reports it. See <see cref="WebUiOptions.RouteAttribute"/>.</summary>
    public string CurrentRoute => RootAttribute(_options.RouteAttribute) ?? string.Empty;

    /// <summary>
    /// The value of the first element carrying <paramref name="attribute"/> — in practice the
    /// app's root, which is where a SPA publishes state of its own (<c>data-user-id</c>, a feature
    /// flag…) for a test to read. Null when no element carries it. Never throws.
    /// </summary>
    public string? RootAttribute(string attribute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attribute);
        return Safe(() => Driver.FindElement(By.CssSelector($"[{attribute}]")).GetAttribute(attribute));
    }

    /// <summary>The browser's current URL.</summary>
    public string CurrentUrl => Safe(() => Driver.Url) ?? string.Empty;

    /// <summary>
    /// Waits until the SPA has settled: navigation complete, no request in flight, DOM flushed.
    /// Every helper that changes what is on screen ends in this.
    /// </summary>
    public bool WaitForAppReady(TimeSpan? timeout = null) =>
        Poll(() => IsReady ? _options.ReadyState : null, timeout) is not null;

    /// <summary>
    /// Waits until a condition holds, on the same polling interval as everything else here.
    ///
    /// <para>For the cases <see cref="WaitForAppReady"/> cannot cover: it asks whether the app
    /// looks idle, which is not the same question as whether the thing you just asked for has
    /// happened.</para>
    /// </summary>
    public bool WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return Poll(() => condition() ? "" : null, timeout) is not null;
    }

    /// <summary>Waits until the SPA is both ready and on the expected route.</summary>
    public bool WaitForRoute(string routeName, TimeSpan? timeout = null) =>
        Poll(() => IsReady && CurrentRoute == routeName ? routeName : null, timeout) is not null;

    private bool IsReady => AppState == _options.ReadyState;

    /// <summary>Where the SPA thinks it is, and what was on screen — appended to every web failure message.</summary>
    internal string Diagnostics() =>
        $"Route: '{CurrentRoute}', app-state: '{AppState}', url: '{CurrentUrl}'. " +
        $"Visible test ids: {string.Join(", ", VisibleTestIds().Take(40))}";

    // ---- Interacting ---------------------------------------------------------------------------

    /// <summary>Taps an element, scrolling it into view first.</summary>
    public void Tap(string testId, TimeSpan? timeout = null) =>
        OnFreshElement(testId, timeout, element =>
        {
            ScrollIntoView(element);
            element.Click();
            return true;
        });

    /// <summary>Taps an element if it is there; returns whether it was.</summary>
    public bool TapIfPresent(string testId, TimeSpan? timeout = null)
    {
        var shortTimeout = timeout ?? _options.ShortTimeout;
        if (WaitOrNull(testId, shortTimeout) is null)
            return false;

        Tap(testId, shortTimeout);
        return true;
    }

    /// <summary>Clears a field and types into it.</summary>
    public void Fill(string testId, string text) =>
        OnFreshElement(testId, null, element =>
        {
            ScrollIntoView(element);
            element.Clear();
            element.SendKeys(text);
            return true;
        });

    /// <summary>The element's visible text.</summary>
    public string TextOf(string testId) => OnFreshElement(testId, null, element =>
    {
        // Scrolled into view before reading, because WebDriver's `text` is defined as the
        // *rendered* text of an element. An element outside the viewport is still found, still
        // reports itself displayed, and hands back an empty string — so a value that is
        // perfectly present reads as blank, and a caller like IntOf fails complaining that
        // there is no number in "". WKWebView is markedly stricter about this than Chrome,
        // which is why it showed up on iOS first and only on longer screens.
        ScrollIntoView(element);
        return element.Text;
    });

    /// <summary>The element's text as an integer, ignoring any non-digits around it.</summary>
    public int IntOf(string testId)
    {
        var text = TextOf(testId);
        var digits = new string([.. text.Where(char.IsDigit)]);

        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"'{testId}' read '{text}', which has no number in it.");
    }

    /// <summary>Whether the element is enabled.</summary>
    public bool IsEnabled(string testId) => OnFreshElement(testId, null, element => element.Enabled);

    /// <summary>Waits for an element's text to equal the expected value.</summary>
    public bool WaitUntilText(string testId, string expected, TimeSpan? timeout = null) =>
        Poll(() =>
        {
            var element = Driver.FindElements(ByTestId(testId)).FirstOrDefault(e => e.Displayed);
            if (element is null)
                return null;

            // Same reason as TextOf: off-screen reads back as empty rather than as its value.
            ScrollIntoView(element);
            return element.Text.Trim() == expected.Trim() ? expected : null;
        }, timeout) is not null;

    /// <summary>
    /// Scrolls an element to the middle of the viewport.
    ///
    /// <para>Via JavaScript rather than a gesture: it behaves identically in a browser and in a
    /// WebView context, whereas the native swipe gestures in <see cref="UiDriver"/> have to
    /// branch per platform and clamp away from system gesture zones.</para>
    /// </summary>
    public IWebElement ScrollIntoView(string testId) => ScrollIntoView(Wait(testId));

    internal IWebElement ScrollIntoView(IWebElement element)
    {
        try
        {
            Script.ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'nearest'})", element);
        }
        catch (WebDriverException ex)
        {
            // Scrolling is an aid, not the assertion. If it fails, let the click try anyway and
            // report its own, more useful, error.
            Log($"[web] scrollIntoView failed: {ex.Message}");
        }

        return element;
    }

    /// <summary>Navigates to a URL relative to the configured base.</summary>
    public void Navigate(string relativeUrl)
    {
        var target = _options.BaseUrl is null
            ? relativeUrl
            : new Uri(new Uri(_options.BaseUrl), relativeUrl).ToString();

        Log($"[web] navigating to {target}");
        Driver.Navigate().GoToUrl(target);
        WaitForAppReady();
    }

    /// <summary>Screenshot for a failure report. Never throws — diagnostics must not add failures.</summary>
    public string? TryCaptureScreenshot(string directory, string name) =>
        Screenshots.TryCapture(Driver, directory, name, _options.Log, "[web]");

    // ---- Plumbing --------------------------------------------------------------------------------

    /// <summary>
    /// Runs an action against a freshly located element, re-locating and retrying whenever the
    /// handle goes stale.
    ///
    /// <para>Locating was always stale-tolerant — <see cref="Poll{T}"/> swallows it — but
    /// <i>acting</i> was not, and that is the half that matters. A handle is a pointer to a
    /// specific DOM node, and SPA frameworks replace nodes rather than mutating them: every re-render is
    /// a chance for the element found a moment ago to no longer be the element on screen. Any
    /// helper that finds once and then acts is therefore racing the framework by
    /// construction.</para>
    ///
    /// <para>Retrying is safe precisely because the driver checks that an element is still
    /// attached <b>before</b> it acts. A <see cref="StaleElementReferenceException"/> means the
    /// click or the keystroke never happened, so repeating it cannot double it.</para>
    /// </summary>
    private T OnFreshElement<T>(string testId, TimeSpan? timeout, Func<IWebElement, T> action)
    {
        var window = timeout ?? _options.DefaultTimeout;
        var deadline = DateTime.UtcNow + window;
        StaleElementReferenceException? lastStale = null;

        do
        {
            try
            {
                return action(Wait(testId, window));
            }
            catch (StaleElementReferenceException ex)
            {
                lastStale = ex;
                Log($"[web] '{testId}' went stale mid-interaction; re-locating");
                Thread.Sleep(_options.PollInterval);
            }
        }
        while (DateTime.UtcNow < deadline);

        throw new WebDriverTimeoutException(
            $"'{testId}' kept going stale for {window.TotalSeconds:0.#}s, so the SPA is re-rendering it " +
            "faster than it can be acted on. " +
            $"Route: '{CurrentRoute}', app-state: '{AppState}', url: '{CurrentUrl}'.",
            lastStale);
    }

    /// <summary>
    /// Polls until the selector yields a non-null value or the timeout expires.
    ///
    /// <para>Swallowing <see cref="WebDriverException"/> covers
    /// <see cref="StaleElementReferenceException"/>, which is not an edge case here: a SPA
    /// framework unmounts and replaces components on every route change, so a handle grabbed a
    /// moment ago goes stale constantly.</para>
    /// </summary>
    private T? Poll<T>(Func<T?> selector, TimeSpan? timeout) where T : class =>
        Retry.Poll(selector, timeout ?? _options.DefaultTimeout, _options.PollInterval);

    private T? Safe<T>(Func<T?> read) where T : class
    {
        try
        {
            return read();
        }
        catch (WebDriverException)
        {
            return null;
        }
    }
}
