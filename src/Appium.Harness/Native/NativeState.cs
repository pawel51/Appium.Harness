using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// How the native trees spell "on" and "enabled". Shared by <see cref="UiDriver"/>'s direct
/// readers and by <see cref="UiElementProbe"/>, which used to carry a copy each — and two copies
/// of a platform quirk is how one of them ends up wrong.
/// </summary>
internal static class NativeState
{
    /// <summary>
    /// Whether the element is enabled. An element that does not report the attribute counts as
    /// enabled: absence of a flag is not a disabled control.
    /// </summary>
    public static bool IsEnabled(IWebElement element) =>
        ParseTrue(element.GetAttribute("enabled"), whenMissing: true);

    /// <summary>
    /// Whether a switch/toggle is on. The attribute differs by platform — Android exposes
    /// <c>checked</c>, iOS <c>value</c> — which is a classic source of a test that passes on
    /// one platform and silently inverts on the other.
    /// </summary>
    public static bool IsToggledOn(IWebElement element, bool isAndroid)
    {
        var raw = (isAndroid ? element.GetAttribute("checked") : element.GetAttribute("value")) ?? NativeText.Of(element);
        return raw is "1" or "true" or "True";
    }

    /// <summary>A platform boolean: <c>true</c>, <c>True</c> or <c>1</c>; <paramref name="whenMissing"/> for null.</summary>
    public static bool ParseTrue(string? raw, bool whenMissing) =>
        raw is null ? whenMissing : raw is "true" or "True" or "1";
}
