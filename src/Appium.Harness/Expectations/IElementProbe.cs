namespace Appium.Harness;

/// <summary>
/// What an <see cref="ElementExpectation"/> needs to know about one element, in terms that mean
/// the same thing native and in a webview. The native and web implementations differ in
/// <i>how</i> they read; the expectation engine above them does not.
///
/// Every read is a fresh look, and throws a <see cref="OpenQA.Selenium.WebDriverException"/> when
/// the element is not there — the engine treats that as "not yet" and polls again.
/// </summary>
internal interface IElementProbe
{
    /// <summary>How failure messages name the element.</summary>
    string Subject { get; }

    /// <summary>The platform's name for the text-colour property, for <see cref="ReadColor"/>.</summary>
    string TextColorName { get; }

    /// <summary>The platform's name for the background-colour property, for <see cref="ReadColor"/>.</summary>
    string BackgroundColorName { get; }

    /// <summary>Whether the element is present and displayed. Absent is simply false.</summary>
    bool IsVisible();

    /// <summary>The element's visible text.</summary>
    string ReadText();

    /// <summary>Whether the element is enabled.</summary>
    bool ReadEnabled();

    /// <summary>Whether a switch / checkbox / toggle is on.</summary>
    bool ReadToggled();

    /// <summary>A raw platform attribute (<c>content-desc</c>, <c>value</c>, a DOM attribute…).</summary>
    string? ReadAttribute(string name);

    /// <summary>A property the app published — a probe payload entry natively, a <c>data-*</c> attribute on the web.</summary>
    string? ReadProperty(string name);

    /// <summary>A colour-valued property, by the platform's name for it. Null when not published.</summary>
    string? ReadColor(string name);

    /// <summary>Context appended to a failure message — what was on screen.</summary>
    string Diagnostics();
}
