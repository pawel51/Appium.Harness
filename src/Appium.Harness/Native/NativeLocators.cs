using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Appium.Harness;

/// <summary>
/// Every per-platform locator the native layer uses, in one place. The platforms name the same
/// thing differently — Android's <c>resource-id</c> is iOS's accessibility id — and a locator
/// that is right on one and silently wrong on the other is the classic cross-platform bug, so
/// the branching lives here and nowhere else.
/// </summary>
internal static class NativeLocators
{
    /// <summary>
    /// An automation id: Android <c>resource-id</c>, iOS accessibility id. MAUI maps
    /// <c>AutomationId</c> to both, which is what makes it the one identifier that survives both.
    /// </summary>
    public static By ById(bool isAndroid, string automationId) =>
        isAndroid ? MobileBy.Id(automationId) : MobileBy.AccessibilityId(automationId);

    /// <summary>
    /// Elements whose <b>simple</b> id matches the whole regex.
    ///
    /// <para>Android's <c>resourceIdMatches</c> tests the <em>fully-qualified</em> resource id
    /// (<c>com.example.app:id/search-result-1</c>) with Java's <c>String.matches</c>, which anchors
    /// at both ends — so a bare pattern silently matches nothing while the element is plainly on
    /// screen. The optional prefix group absorbs the package qualifier. iOS's <c>name</c> is
    /// already the bare accessibility id.</para>
    /// </summary>
    public static By ByIdRegex(bool isAndroid, string idRegex) =>
        isAndroid
            ? MobileBy.AndroidUIAutomator($"new UiSelector().resourceIdMatches(\"(.*/)?{idRegex}\")")
            : MobileBy.IosNSPredicate($"name MATCHES '{idRegex}'");

    /// <summary>A visible element by its exact text — for platform chrome that carries no automation id.</summary>
    public static By ByText(bool isAndroid, string text) =>
        isAndroid
            ? MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{text}\")")
            : MobileBy.IosNSPredicate($"name == '{text}' AND visible == 1");

    /// <summary>Every element in the tree, for diagnostics.</summary>
    public static By Everything => MobileBy.XPath("//*");

    /// <summary>The attribute that carries the automation id.</summary>
    public static string IdAttribute(bool isAndroid) => isAndroid ? "resource-id" : "name";

    /// <summary>Android reports <c>com.example.app:id/foo</c>; the id the app set is <c>foo</c>.</summary>
    public static string SimpleId(string id) =>
        id.Contains(':') ? id[(id.LastIndexOf('/') + 1)..] : id;
}
