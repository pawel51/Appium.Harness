namespace Appium.Harness;

// The hybrid seam: crossing from the native tree into an embedded WebView. This type stays a
// native-only surface; the web surface is WebUi, reached through the returned context.
public sealed partial class UiDriver
{
    /// <summary>
    /// Switches this session into the app's embedded WebView and hands back a
    /// <see cref="WebViewContext"/> whose disposal returns it to the native tree.
    ///
    /// <para>Use it with <c>using</c> so a failing assertion still leaves the session in the
    /// native context for the next step.</para>
    ///
    /// <code>
    /// using var web = Ui.EnterWebView("http://10.0.2.2:5081", "com.example.app");
    /// web.Web.Tap("lesson-mark-complete");
    /// </code>
    /// </summary>
    /// <param name="expectedUrlPrefix">Base URL of the embedded app, used to pick the right window.</param>
    /// <param name="packageHint">The app package, which names the context on Android. Defaults to <see cref="AppiumHarnessOptions.AppId"/>.</param>
    /// <param name="timeout">How long to wait for a webview context to appear.</param>
    public WebViewContext EnterWebView(
        string? expectedUrlPrefix = null,
        string? packageHint = null,
        TimeSpan? timeout = null) =>
        WebViewContext.Enter(this, expectedUrlPrefix, packageHint ?? _options.AppId, timeout, _options.Log, _options.FailureScreenshotDirectory);
}
