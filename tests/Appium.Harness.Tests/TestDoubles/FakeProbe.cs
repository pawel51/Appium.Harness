using OpenQA.Selenium;

namespace Appium.Harness.Tests;

/// <summary>An element whose every read is a delegate the test controls — no device, no driver.</summary>
internal sealed class FakeProbe : IElementProbe
{
    public string Subject { get; set; } = "'thing'";

    public string TextColorName { get; set; } = "TextColor";

    public string BackgroundColorName { get; set; } = "BackgroundColor";

    public Func<bool> Visible { get; set; } = () => true;

    public Func<string> Text { get; set; } = () => "hello";

    public Func<bool> Enabled { get; set; } = () => true;

    public Func<bool> Toggled { get; set; } = () => false;

    public Func<string, string?> Attribute { get; set; } = _ => null;

    public Func<string, string?> Property { get; set; } = _ => null;

    public Func<string, string?> Color { get; set; } = _ => null;

    public Func<string> DiagnosticsText { get; set; } = () => "Visible ids: a, b";

    public int TextReads { get; private set; }

    public bool IsVisible() => Visible();

    public string ReadText()
    {
        TextReads++;
        return Text();
    }

    public bool ReadEnabled() => Enabled();

    public bool ReadToggled() => Toggled();

    public string? ReadAttribute(string name) => Attribute(name);

    public string? ReadProperty(string name) => Property(name);

    public string? ReadColor(string name) => Color(name);

    public string Diagnostics() => DiagnosticsText();

    /// <summary>A reader that is wrong for the first <paramref name="badReads"/> calls, then right.</summary>
    public static Func<T> Eventually<T>(int badReads, T bad, T good)
    {
        var reads = 0;
        return () => ++reads <= badReads ? bad : good;
    }

    /// <summary>A reader that behaves like an element that is not in the tree.</summary>
    public static Func<T> Absent<T>() => () => throw new NoSuchElementException("no such element: not in the tree");
}

internal static class Settings
{
    public static ExpectSettings Fast(
        int timeoutMs = 120,
        Action<string>? log = null,
        Func<string, string?>? screenshot = null) =>
        new(TimeSpan.FromMilliseconds(timeoutMs), TimeSpan.FromMilliseconds(5), log, screenshot);
}
