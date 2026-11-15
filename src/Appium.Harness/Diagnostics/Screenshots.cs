using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// Failure evidence. One implementation for the native and the web layer, which used to carry a
/// copy each — both drive an <see cref="IWebDriver"/>, so there is nothing to tell them apart.
/// </summary>
internal static class Screenshots
{
    /// <summary>Where failure screenshots go when no folder is configured: the OS temp folder, which CI discards.</summary>
    public static string DefaultDirectory => Path.Combine(Path.GetTempPath(), "appium-harness-failures");

    /// <summary>
    /// Captures a PNG into <paramref name="directory"/>, returning its path, or null if the driver
    /// cannot take one or the capture failed. Never throws — diagnostics must not add failures.
    /// </summary>
    public static string? TryCapture(IWebDriver driver, string directory, string name, Action<string>? log, string logPrefix)
    {
        try
        {
            if (driver is not ITakesScreenshot shooter)
            {
                return null;
            }

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{SanitiseFileName(name)}.png");
            shooter.GetScreenshot().SaveAsFile(path);
            return path;
        }
        catch (Exception ex) when (ex is WebDriverException or IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"{logPrefix} screenshot '{name}' failed: {ex.Message}");
            return null;
        }
    }

    /// <summary><paramref name="value"/> with every character a file name cannot hold replaced by <c>_</c>.</summary>
    public static string SanitiseFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
