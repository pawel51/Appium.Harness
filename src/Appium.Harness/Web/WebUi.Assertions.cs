using OpenQA.Selenium;

namespace Appium.Harness;

// Assertions and the readers behind them — the web twin of UiDriver.Assertions.cs.
public sealed partial class WebUi
{
    #region Expecting

    /// <summary>
    /// Assertions about one element that wait for the condition to hold, then throw a
    /// <see cref="HarnessAssertionException"/> saying what was expected and what was seen.
    /// Same shape as <c>UiDriver.Expect</c>; colours are read from the computed style.
    ///
    /// <code>
    /// web.Expect(WebTestIds.ProfileTotalXp).HasText("120").HasTextColor("rgb(126, 180, 240)");
    /// </code>
    /// </summary>
    public ElementExpectation Expect(string testId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testId);
        return new ElementExpectation(new WebElementProbe(this, testId), CreateExpectSettings());
    }

    /// <summary>Assertions about how many elements match a CSS selector — the rows in a list.</summary>
    /// <param name="cssSelector">Any CSS selector, e.g. <c>"[data-testid^='lesson-row-']"</c>.</param>
    public CountExpectation ExpectCount(string cssSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cssSelector);
        return new CountExpectation(
            $"'{cssSelector}'",
            () => Count(cssSelector),
            Diagnostics,
            CreateExpectSettings());
    }

    /// <summary>Assertions about how many elements carry a test id starting with <paramref name="testIdPrefix"/>.</summary>
    public CountExpectation ExpectCountWithPrefix(string testIdPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testIdPrefix);
        return new CountExpectation(
            $"test ids starting '{testIdPrefix}'",
            () => CountTestIds(testIdPrefix),
            Diagnostics,
            CreateExpectSettings());
    }

    private ExpectSettings CreateExpectSettings() => new(
        _options.DefaultTimeout,
        _options.PollInterval,
        _options.Log,
        name => TryCaptureScreenshot(_options.FailureScreenshotDirectory ?? Screenshots.DefaultDirectory, name));

    #endregion

    #region Reading

    /// <summary>How many elements match a CSS selector right now (displayed or not).</summary>
    public int Count(string cssSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cssSelector);
        return Driver.FindElements(By.CssSelector(cssSelector)).Count;
    }

    /// <summary>How many elements carry a test id starting with <paramref name="testIdPrefix"/>.</summary>
    public int CountTestIds(string testIdPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testIdPrefix);
        return Count($"[{_options.TestIdAttribute}^='{testIdPrefix.Replace("'", "\\'")}']");
    }

    /// <summary>
    /// A computed colour, parsed — the colour the user actually sees, after stylesheets,
    /// inheritance and theme variables. Throws <see cref="FormatException"/> if the value is not
    /// a colour this harness understands.
    /// </summary>
    /// <param name="testId">The element.</param>
    /// <param name="cssProperty">A CSS colour property; <c>"color"</c> by default.</param>
    public HarnessColor ColorOf(string testId, string cssProperty = "color") =>
        HarnessColor.Parse(Find(testId).GetCssValue(cssProperty));

    /// <summary>A <c>data-*</c> attribute (pass the name without the prefix) or, failing that, a DOM property. Null if neither exists.</summary>
    public string? PropertyOf(string testId, string name)
    {
        var element = Find(testId);
        return element.GetDomAttribute($"data-{name}") ?? element.GetDomProperty(name);
    }

    /// <summary>Any DOM attribute, or failing that DOM property, by its exact name. Null if neither exists.</summary>
    public string? AttributeOf(string testId, string name)
    {
        var element = Find(testId);
        return element.GetDomAttribute(name) ?? element.GetDomProperty(name);
    }

    #endregion
}
