namespace Appium.Harness;

/// <summary>
/// Everything needed to open an Appium session, in one place.
///
/// The distinction that drives the whole type is <see cref="IsLocal"/>: against a cloud grid
/// (BrowserStack and friends) the vendor SDK merges the app, device and server URI into the
/// capabilities at run time, so the test code must set <em>none</em> of them. Locally nothing
/// does that, so they have to come from somewhere — here.
/// </summary>
public sealed class AppiumHarnessOptions
{
    /// <summary>Target platform. Selects the driver and the per-platform locator strategy.</summary>
    public MobilePlatform Platform { get; init; } = MobilePlatform.Android;

    /// <summary>
    /// Path to the .apk / .app under test. <b>Leave null for a cloud run</b> — the grid's SDK
    /// supplies it. A non-null value is what puts the harness in local mode.
    /// </summary>
    public string? AppPath { get; init; }

    /// <summary>Device or simulator name. Ignored on a cloud run (the grid config wins).</summary>
    public string? DeviceName { get; init; }

    /// <summary>OS version. Mostly useful for pinning an iOS simulator locally.</summary>
    public string? PlatformVersion { get; init; }

    /// <summary>Appium server base URI. Cloud SDKs rewrite this to their hub.</summary>
    public Uri ServerUri { get; init; } = new("http://127.0.0.1:4723/");

    /// <summary>
    /// App bundle id / package name. Only needed for lifecycle operations
    /// (<see cref="UiDriver.RestartApp"/>); element interaction does not use it.
    /// </summary>
    public string? AppId { get; init; }

    /// <summary>Default timeout for the waiting helpers.</summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>How often the waiting helpers re-check. Shorter costs more round trips.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Automation engine. Defaults per platform (UIAutomator2 / XCUITest). Worth pinning
    /// explicitly on a cloud grid — an unpinned engine is a common cause of an empty UI tree.
    /// </summary>
    public string? AutomationName { get; init; }

    /// <summary>Extra capabilities merged verbatim into the options.</summary>
    public IReadOnlyDictionary<string, object>? AdditionalCapabilities { get; init; }

    /// <summary>Optional sink for harness diagnostics. Wire to your test framework's output.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>
    /// Folder for the screenshot taken when an <c>Expect</c> times out. Null falls back to
    /// <see cref="DefaultFailureScreenshotDirectory"/> in the OS temp folder, which CI discards —
    /// pass a path inside the test project to keep the files.
    /// </summary>
    public string? FailureScreenshotDirectory { get; init; }

    /// <summary>Where failure screenshots go when <see cref="FailureScreenshotDirectory"/> is not set.</summary>
    public static string DefaultFailureScreenshotDirectory => Screenshots.DefaultDirectory;

    /// <summary>True when this session supplies its own app — i.e. no cloud SDK is doing it.</summary>
    public bool IsLocal => !string.IsNullOrWhiteSpace(AppPath);

    /// <summary>
    /// Builds options from environment variables, the convention this harness ships with:
    /// <c>APPIUM_APP</c> (presence enables local mode), <c>APPIUM_DEVICE</c>,
    /// <c>APPIUM_PLATFORM_VERSION</c>, <c>APPIUM_SERVER</c>.
    /// With none set you get cloud-mode options, which is exactly what a grid SDK expects.
    /// </summary>
    /// <param name="platform">Which platform to drive.</param>
    /// <param name="defaultDeviceName">Device name when <c>APPIUM_DEVICE</c> is not set.</param>
    /// <param name="appId">Bundle id / package name, for app restarts and webview context matching.</param>
    /// <param name="log">Where to write session diagnostics.</param>
    /// <param name="additionalCapabilities">
    /// Extra capabilities to merge in — e.g. the chromedriver settings an embedded WebView
    /// needs. Optional so existing callers are unaffected.
    /// </param>
    /// <param name="failureScreenshotDirectory">See <see cref="FailureScreenshotDirectory"/>.</param>
    public static AppiumHarnessOptions FromEnvironment(
        MobilePlatform platform,
        string defaultDeviceName,
        string? appId = null,
        Action<string>? log = null,
        IReadOnlyDictionary<string, object>? additionalCapabilities = null,
        string? failureScreenshotDirectory = null)
    {
        var app = Environment.GetEnvironmentVariable("APPIUM_APP");
        var server = Environment.GetEnvironmentVariable("APPIUM_SERVER");

        return new AppiumHarnessOptions
        {
            Platform = platform,
            AppId = appId,
            Log = log,
            AppPath = string.IsNullOrWhiteSpace(app) ? null : Path.GetFullPath(app),
            DeviceName = Environment.GetEnvironmentVariable("APPIUM_DEVICE") ?? defaultDeviceName,
            PlatformVersion = Environment.GetEnvironmentVariable("APPIUM_PLATFORM_VERSION"),
            ServerUri = string.IsNullOrWhiteSpace(server) ? new Uri("http://127.0.0.1:4723/") : new Uri(server),
            AdditionalCapabilities = additionalCapabilities,
            FailureScreenshotDirectory = failureScreenshotDirectory,
        };
    }
}
