namespace Appium.Harness;

/// <summary>Builds the body of a failure message, shared by element and count expectations.</summary>
internal static class Failure
{
    public static string Describe(
        string headline,
        int attempts,
        TimeSpan elapsed,
        string diagnostics,
        ExpectSettings settings,
        string screenshotName)
    {
        var lines = new List<string>
        {
            headline,
            $"Gave up after {elapsed.TotalSeconds:0.##}s ({attempts} {(attempts == 1 ? "attempt" : "attempts")}, timeout {settings.Timeout.TotalSeconds:0.##}s)."
        };

        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            lines.Add(diagnostics);
        }

        string? screenshot = null;
        try
        {
            screenshot = settings.CaptureScreenshot?.Invoke($"expect-failed-{screenshotName}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Evidence-gathering must never replace the real failure with its own.
        }

        if (screenshot is not null)
        {
            lines.Add($"Screenshot: {screenshot}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
