using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// The few things the native readers need from a live session — a seam so the counting, probe
/// reading and expectation wiring can be exercised without a device. <see cref="UiDriver"/> is
/// the only production implementation.
/// </summary>
internal interface INativeScreen
{
    bool IsAndroid { get; }

    /// <summary>Finds an element now, throwing <see cref="NoSuchElementException"/> if absent.</summary>
    IWebElement Find(string automationId);

    /// <summary>Whether the element is present and displayed right now. Never throws.</summary>
    bool Exists(string automationId);

    /// <summary>Elements whose simple id matches the whole regex.</summary>
    IReadOnlyList<IWebElement> FindByIdRegex(string idRegex);

    IReadOnlyList<string> VisibleIds();

    string PageSource();

    void SwipeUp();

    void SwipeDown();
}
