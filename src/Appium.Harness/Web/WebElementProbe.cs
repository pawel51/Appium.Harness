using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// Reads one DOM element for an <see cref="ElementExpectation"/>. The web needs no probe payload:
/// colours come from the computed style, and the app's own values from <c>data-*</c> attributes
/// or DOM properties — the SPA already publishes its state that way.
/// </summary>
internal sealed class WebElementProbe(WebUi web, string testId) : IElementProbe
{
    public string Subject => $"'{testId}'";

    public string TextColorName => "color";

    public string BackgroundColorName => "background-color";

    public bool IsVisible()
    {
        try
        {
            return web.Driver.FindElements(web.ByTestId(testId)).Any(e => e.Displayed);
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    public string ReadText()
    {
        // Scrolled into view first: WebDriver's text is the *rendered* text, and an element
        // outside the viewport reads back empty (see WebUi.TextOf).
        var element = web.ScrollIntoView(web.Find(testId));
        return element.Text;
    }

    public bool ReadEnabled() => web.Find(testId).Enabled;

    public bool ReadToggled()
    {
        var element = web.Find(testId);
        return element.Selected
            || element.GetDomAttribute("aria-checked") == "true"
            || element.GetDomAttribute("aria-pressed") == "true";
    }

    public string? ReadAttribute(string name)
    {
        var element = web.Find(testId);
        return element.GetDomAttribute(name) ?? element.GetDomProperty(name);
    }

    public string? ReadProperty(string name)
    {
        var element = web.Find(testId);
        return element.GetDomAttribute($"data-{name}") ?? element.GetDomProperty(name);
    }

    // The *computed* value, so it is the colour the user sees — after stylesheets, inheritance
    // and theme variables — not whatever an inline style happened to say.
    public string? ReadColor(string name) => web.Find(testId).GetCssValue(name);

    public string Diagnostics() => web.Diagnostics();
}
