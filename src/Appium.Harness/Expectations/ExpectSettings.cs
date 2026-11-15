namespace Appium.Harness;

/// <summary>Timing, logging and failure-evidence settings shared by every expectation.</summary>
internal sealed record ExpectSettings(
    TimeSpan Timeout,
    TimeSpan PollInterval,
    Action<string>? Log,
    Func<string, string?>? CaptureScreenshot);
