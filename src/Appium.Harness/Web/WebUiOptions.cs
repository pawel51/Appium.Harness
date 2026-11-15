namespace Appium.Harness;

/// <summary>Timeouts, polling, logging and the page's readiness contract for <see cref="WebUi"/>.</summary>
public sealed class WebUiOptions
{
    /// <summary>Base URL that <see cref="WebUi.Navigate"/> resolves against.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>How long a wait persists before failing. Matches the native harness's default.</summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Timeout for existence checks, where the answer is often "no" and waiting is waste.</summary>
    public TimeSpan ShortTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Gap between polls.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Where to write diagnostics. Null to stay quiet.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>
    /// Folder for the screenshot taken when an <c>Expect</c> times out. Null falls back to
    /// <see cref="AppiumHarnessOptions.DefaultFailureScreenshotDirectory"/> in the OS temp folder, which CI discards —
    /// pass a path inside the test project to keep the files.
    /// </summary>
    public string? FailureScreenshotDirectory { get; init; }

    // ---- The page's contract --------------------------------------------------------------
    //
    // A single-page app has no page loads to wait on, so the harness waits on attributes the app
    // publishes instead. The defaults are the conventions this harness ships with; change them to
    // match an app that spells them differently.

    /// <summary>The attribute every element a test addresses carries. Default <c>data-testid</c>.</summary>
    public string TestIdAttribute { get; init; } = "data-testid";

    /// <summary>
    /// The attribute the app sets on its root element to say what it is doing — booting, loading,
    /// ready, error. Default <c>data-app-state</c>.
    /// </summary>
    public string AppStateAttribute { get; init; } = "data-app-state";

    /// <summary>The value of <see cref="AppStateAttribute"/> that means "settled". Default <c>ready</c>.</summary>
    public string ReadyState { get; init; } = "ready";

    /// <summary>The attribute the app sets on its root element to name the current route. Default <c>data-route</c>.</summary>
    public string RouteAttribute { get; init; } = "data-route";
}
