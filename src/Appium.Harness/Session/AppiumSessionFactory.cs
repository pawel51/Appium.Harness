using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace Appium.Harness;

/// <summary>
/// Creates the <see cref="AppiumDriver"/> for a session.
///
/// Split from <see cref="UiDriver"/> so the interaction API can be unit-tested or wrapped
/// around a driver you created yourself — a cloud vendor's SDK, a custom capability set, or a
/// driver shared across a fixture.
/// </summary>
public static class AppiumSessionFactory
{
    /// <summary>Opens a session and wraps it in a <see cref="UiDriver"/>.</summary>
    public static UiDriver Start(AppiumHarnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new UiDriver(CreateDriver(options), options);
    }

    /// <summary>Opens a raw <see cref="AppiumDriver"/> without the helper layer.</summary>
    public static AppiumDriver CreateDriver(AppiumHarnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var appiumOptions = BuildCapabilities(options);

        options.Log?.Invoke(
            $"[harness] {appiumOptions.PlatformName}/{appiumOptions.AutomationName} " +
            $"{(options.IsLocal ? $"local app={options.AppPath}" : "cloud (capabilities from grid SDK)")}");

        return options.Platform == MobilePlatform.Android
            ? new AndroidDriver(options.ServerUri, appiumOptions)
            : new IOSDriver(options.ServerUri, appiumOptions);
    }

    /// <summary>
    /// The capabilities a session is opened with — what to ask for, separate from opening the
    /// connection, so it can be checked without an Appium server.
    /// </summary>
    internal static AppiumOptions BuildCapabilities(AppiumHarnessOptions options)
    {
        var appiumOptions = new AppiumOptions
        {
            PlatformName = options.Platform == MobilePlatform.Android ? "Android" : "iOS",
            AutomationName = options.AutomationName
                ?? (options.Platform == MobilePlatform.Android ? "UIAutomator2" : "XCUITest"),
        };

        if (options.Platform == MobilePlatform.Android)
        {
            // Runtime permission dialogs are modal and will swallow the first taps of a test.
            appiumOptions.AddAdditionalAppiumOption("autoGrantPermissions", "true");
        }
        else
        {
            appiumOptions.AddAdditionalAppiumOption("autoAcceptAlerts", "true");
        }

        // Local mode only. On a cloud grid the vendor SDK injects app/device/version, and
        // setting them here would override the grid's own configuration.
        if (options.IsLocal)
        {
            // Fail here, with the working directory spelled out. A relative APPIUM_APP is
            // resolved against the *test host's* cwd — which for `dotnet test` is the build
            // output directory, not the project — so a sensible-looking "../../../src/…"
            // silently resolves inside bin/. Appium's own error arrives only after session
            // negotiation and says nothing about what it resolved or from where.
            if (!File.Exists(options.AppPath) && !Directory.Exists(options.AppPath))
            {
                throw new FileNotFoundException(
                    $"App not found: '{options.AppPath}'. Relative paths resolve against the " +
                    $"current directory, which here is '{Directory.GetCurrentDirectory()}' — " +
                    "prefer an absolute path for APPIUM_APP.",
                    options.AppPath);
            }

            appiumOptions.App = options.AppPath;

            if (!string.IsNullOrWhiteSpace(options.DeviceName))
            {
                appiumOptions.DeviceName = options.DeviceName;
            }

            if (!string.IsNullOrWhiteSpace(options.PlatformVersion))
            {
                appiumOptions.PlatformVersion = options.PlatformVersion;
            }
        }

        if (options.AdditionalCapabilities is { Count: > 0 })
        {
            foreach (var (key, value) in options.AdditionalCapabilities)
            {
                appiumOptions.AddAdditionalAppiumOption(key, value);
            }
        }

        return appiumOptions;
    }
}
