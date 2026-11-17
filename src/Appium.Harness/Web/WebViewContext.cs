using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Interfaces;

namespace Appium.Harness;

/// <summary>
/// Switches a live Appium session between the native tree and an embedded WebView, and puts it
/// back on dispose.
///
/// <para>This is the hybrid seam. Inside the <c>using</c>, the <i>same driver object</i>
/// answers CSS queries against the WebView's DOM; outside it, the same driver answers native
/// automation-id queries again. Which is what lets a single test complete a lesson in the web
/// tab, drop back to native, and assert the XP rose on the native profile — one session, both
/// surfaces, one database.</para>
///
/// <para><b>If no webview context appears, the cause is almost never Appium.</b> It is that the
/// app under test has no <i>inspectable</i> WebView, so there is no DevTools socket to attach
/// to. On Android that means <c>WebView.SetWebContentsDebuggingEnabled(true)</c> did not run —
/// typically a Release build. On iOS 16.4+ it means
/// <c>WKWebView.Inspectable</c> was left false. The exception thrown here names the contexts
/// that <i>were</i> available so the message says that, rather than "no such element".</para>
/// </summary>
public sealed class WebViewContext : IDisposable
{
    /// <summary>The native context, which every session starts in.</summary>
    public const string NativeContext = "NATIVE_APP";

    /// <summary>Prefix every embedded-webview context name carries, on both platforms.</summary>
    public const string WebViewPrefix = "WEBVIEW_";

    private readonly IContextAware _contextAware;
    private readonly Action<string>? _log;
    private bool _restored;

    private WebViewContext(AppiumDriver driver, string contextName, WebUi web, Action<string>? log)
    {
        _contextAware = driver;
        _log = log;
        ContextName = contextName;
        Web = web;
    }

    /// <summary>The context that was entered, e.g. <c>WEBVIEW_com.example.app</c>.</summary>
    public string ContextName { get; }

    /// <summary>The web element layer, bound to the same driver.</summary>
    public WebUi Web { get; }

    /// <summary>
    /// Enters the app's webview context and waits for the SPA to report itself ready.
    /// </summary>
    /// <param name="ui">The live native session.</param>
    /// <param name="expectedUrlPrefix">
    /// Base URL of the embedded app. Used to pick the right window — a MAUI app can present
    /// several, and the wrong one yields an empty document. Checked even when only one window
    /// is exposed, so the session is always switched onto a window explicitly.
    /// </param>
    /// <param name="packageHint">
    /// Android names contexts by package (<c>WEBVIEW_com.example.app</c>); iOS names
    /// them by handle (<c>WEBVIEW_1</c>). Passing the package makes the Android choice exact.
    /// </param>
    /// <param name="timeout">How long to wait for a context to appear. The page has to load first.</param>
    /// <param name="log">Where to write diagnostics.</param>
    /// <param name="failureScreenshotDirectory">See <see cref="WebUiOptions.FailureScreenshotDirectory"/>.</param>
    public static WebViewContext Enter(
        UiDriver ui,
        string? expectedUrlPrefix = null,
        string? packageHint = null,
        TimeSpan? timeout = null,
        Action<string>? log = null,
        string? failureScreenshotDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(ui);

        var driver = ui.Driver;
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var seen = Array.Empty<string>();

        // The webview only registers once the page has begun loading, so this polls rather
        // than reading once — the tab was tapped moments ago.
        while (DateTime.UtcNow < deadline)
        {
            seen = [.. ((IContextAware)driver).Contexts];

            var target = Choose(seen, packageHint);
            if (target is not null)
            {
                log?.Invoke($"[webview] entering '{target}' (contexts: {string.Join(", ", seen)})");
                ((IContextAware)driver).Context = target;

                var web = new WebUi(driver, new WebUiOptions { BaseUrl = expectedUrlPrefix, Log = log, FailureScreenshotDirectory = failureScreenshotDirectory });
                SelectWindow(driver, expectedUrlPrefix, log);

                if (!web.WaitForAppReady(TimeSpan.FromSeconds(20)))
                {
                    log?.Invoke(
                        $"[webview] entered '{target}' but the app never reported ready " +
                        $"(state '{web.AppState}', url '{web.CurrentUrl}'). Continuing anyway — " +
                        "the test's own wait will produce the better message.");
                }

                return new WebViewContext(driver, target, web, log);
            }

            Thread.Sleep(500);
        }

        throw new WebDriverException(
            $"No '{WebViewPrefix}*' context appeared within {(timeout ?? TimeSpan.FromSeconds(30)).TotalSeconds:0}s. " +
            $"Available contexts: [{string.Join(", ", seen)}]. " +
            "The usual cause is an app with no inspectable WebView: on Android, build with " +
            "a build that calls WebView.SetWebContentsDebuggingEnabled(true) (a Debug build usually does); " +
            "on iOS 16.4+, WKWebView.Inspectable must be true. Confirm with chrome://inspect/#devices " +
            "before adjusting any Appium capability.");
    }

    /// <summary>Prefers the app's own package, then any webview context.</summary>
    internal static string? Choose(IReadOnlyList<string> contexts, string? packageHint)
    {
        if (packageHint is { Length: > 0 })
        {
            var exact = contexts.FirstOrDefault(c =>
                c.StartsWith(WebViewPrefix, StringComparison.Ordinal)
                && c.Contains(packageHint, StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
                return exact;
        }

        return contexts.FirstOrDefault(c => c.StartsWith(WebViewPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Picks the window actually showing the embedded app.
    ///
    /// <para>A single webview context can expose several window handles, and the first is not
    /// reliably the right one — landing on the wrong handle yields an empty
    /// <c>&lt;html&gt;</c>, which then presents as every selector failing at once.</para>
    /// </summary>
    private static void SelectWindow(AppiumDriver driver, string? expectedUrlPrefix, Action<string>? log)
    {
        try
        {
            if (expectedUrlPrefix is not { Length: > 0 })
                return;

            foreach (var handle in driver.WindowHandles)
            {
                driver.SwitchTo().Window(handle);
                if (driver.Url.StartsWith(expectedUrlPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke($"[webview] selected window '{handle}' at {driver.Url}");
                    return;
                }
            }

            log?.Invoke(
                $"[webview] no window matched '{expectedUrlPrefix}'; staying on '{driver.CurrentWindowHandle}'.");
        }
        catch (WebDriverException ex)
        {
            log?.Invoke($"[webview] window selection skipped: {ex.Message}");
        }
    }

    /// <summary>Returns the session to the native tree. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_restored)
            return;

        _restored = true;

        try
        {
            _contextAware.Context = NativeContext;
            _log?.Invoke("[webview] back to NATIVE_APP");
        }
        catch (WebDriverException ex)
        {
            // The session may already be gone if the test failed hard. Restoring context is a
            // courtesy to whatever runs next, not something worth masking the real failure for.
            _log?.Invoke($"[webview] could not restore native context: {ex.Message}");
        }
    }
}
