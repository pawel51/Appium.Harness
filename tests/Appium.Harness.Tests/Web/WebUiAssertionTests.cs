using System.Collections.ObjectModel;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

/// <summary>The web half: <see cref="WebUi"/> over a faked <see cref="IWebDriver"/> — no browser, no webview.</summary>
[TestFixture]
public class WebUiAssertionTests
{
    private IWebDriver _driver = null!;
    private WebUi _web = null!;
    private List<string> _log = null!;

    [SetUp]
    public void SetUp()
    {
        _driver = Substitute.For<IWebDriver, IJavaScriptExecutor, ITakesScreenshot>();
        _driver.FindElements(Arg.Any<By>()).Returns(new ReadOnlyCollection<IWebElement>([]));
        _log = [];
        _web = new WebUi(_driver, new WebUiOptions
        {
            DefaultTimeout = TimeSpan.FromMilliseconds(120),
            PollInterval = TimeSpan.FromMilliseconds(5),
            Log = _log.Add,
        });
    }

    [TearDown]
    public void TearDown() => _driver.Dispose();

    private IWebElement Element(
        string testId,
        string text = "",
        bool displayed = true,
        bool enabled = true,
        bool selected = false,
        Dictionary<string, string>? css = null,
        Dictionary<string, string>? attributes = null,
        Dictionary<string, string>? properties = null)
    {
        var element = Substitute.For<IWebElement>();
        element.Text.Returns(text);
        element.Displayed.Returns(displayed);
        element.Enabled.Returns(enabled);
        element.Selected.Returns(selected);
        element.GetCssValue(Arg.Any<string>()).Returns(call => css is not null && css.TryGetValue(call.Arg<string>(), out var v) ? v : "");
        element.GetDomAttribute(Arg.Any<string>()).Returns(call => attributes is not null && attributes.TryGetValue(call.Arg<string>(), out var v) ? v : null);
        element.GetDomProperty(Arg.Any<string>()).Returns(call => properties is not null && properties.TryGetValue(call.Arg<string>(), out var v) ? v : null);

        _driver.FindElement(Arg.Is<By>(b => b.ToString()!.Contains($"data-testid='{testId}'"))).Returns(element);
        _driver.FindElements(Arg.Is<By>(b => b.ToString()!.Contains($"data-testid='{testId}'")))
            .Returns(new ReadOnlyCollection<IWebElement>([element]));
        return element;
    }

    private void Select(string cssFragment, int count)
    {
        var elements = Enumerable.Range(0, count).Select(_ => Substitute.For<IWebElement>()).ToList();
        _driver.FindElements(Arg.Is<By>(b => b.ToString()!.Contains(cssFragment)))
            .Returns(new ReadOnlyCollection<IWebElement>(elements));
    }

    private static string FailureOf(TestDelegate act) =>
        Assert.Throws<HarnessAssertionException>(act)!.Message;

    #region Expect

    [Test]
    public void Expect_asserts_text_visibility_and_state()
    {
        Element("web-xp", text: "120");
        Element("web-save", enabled: false);

        Assert.DoesNotThrow(() => _web.Expect("web-xp").IsVisible().HasText("120").IsEnabled());
        Assert.DoesNotThrow(() => _web.Expect("web-save").Not.IsEnabled());
    }

    [Test]
    public void Reading_text_scrolls_the_element_into_view_first()
    {
        var element = Element("web-xp", text: "120");

        _web.Expect("web-xp").HasText("120");

        ((IJavaScriptExecutor)_driver).Received().ExecuteScript(Arg.Is<string>(s => s.Contains("scrollIntoView")), element);
    }

    [Test]
    public void A_failure_carries_the_route_the_spa_state_and_the_test_ids_on_screen()
    {
        Element("web-xp", text: "0");

        var message = FailureOf(() => _web.Expect("web-xp").HasText("120"));

        Assert.That(message, Does.Contain("Expected 'web-xp' to have text '120'").And.Contain("it was '0'"));
        Assert.That(message, Does.Contain("Route:").And.Contain("app-state:").And.Contain("Visible test ids:"));
    }

    [Test]
    public void Expect_waits_for_text_that_arrives_late()
    {
        var element = Element("web-xp");
        var reads = 0;
        element.Text.Returns(_ => ++reads < 4 ? "0" : "120");

        Assert.DoesNotThrow(() => _web.Expect("web-xp").WithTimeout(TimeSpan.FromSeconds(2)).HasText("120"));
    }

    [Test]
    public void IsVisible_is_false_for_a_hidden_missing_or_unqueryable_element()
    {
        Element("web-hidden", displayed: false);

        Assert.DoesNotThrow(() => _web.Expect("web-hidden").Not.IsVisible());
        Assert.DoesNotThrow(() => _web.Expect("web-missing").Not.IsVisible());
        Assert.Throws<HarnessAssertionException>(() => _web.Expect("web-missing").IsVisible());

        _driver.FindElements(Arg.Any<By>()).Throws(new WebDriverException("context gone"));
        Assert.DoesNotThrow(() => _web.Expect("web-anything").Not.IsVisible());
    }

    [Test]
    public void A_missing_element_cannot_satisfy_a_text_check_either_way()
    {
        _driver.FindElement(Arg.Any<By>()).Throws(new NoSuchElementException("no such element"));

        var message = FailureOf(() => _web.Expect("web-ghost").HasText("x"));

        Assert.That(message, Does.Contain("could not be read"));
        Assert.Throws<HarnessAssertionException>(() => _web.Expect("web-ghost").Not.HasText("x"));
    }

    [TestCase(true, null, null, true)]
    [TestCase(false, "true", null, true)]
    [TestCase(false, null, "true", true)]
    [TestCase(false, "false", "false", false)]
    [TestCase(false, null, null, false)]
    public void Toggled_reads_selected_then_aria_checked_then_aria_pressed(bool selected, string? ariaChecked, string? ariaPressed, bool expected)
    {
        var attributes = new Dictionary<string, string>();
        if (ariaChecked is not null) attributes["aria-checked"] = ariaChecked;
        if (ariaPressed is not null) attributes["aria-pressed"] = ariaPressed;
        Element("web-dark", selected: selected, attributes: attributes);

        if (expected) Assert.DoesNotThrow(() => _web.Expect("web-dark").IsToggledOn());
        else Assert.DoesNotThrow(() => _web.Expect("web-dark").Not.IsToggledOn());
    }

    #endregion

    #region Colour

    [Test]
    public void Colours_come_from_the_computed_style_and_compare_with_hex()
    {
        Element("web-xp", css: new() { ["color"] = "rgb(126, 180, 240)", ["background-color"] = "rgba(0, 0, 0, 0)" });

        Assert.DoesNotThrow(() => _web.Expect("web-xp").HasTextColor("#7EB4F0").HasBackgroundColor("transparent"));
    }

    [Test]
    public void Any_css_colour_property_can_be_asserted_by_name()
    {
        Element("web-card", css: new() { ["border-top-color"] = "rgb(255, 0, 0)" });

        Assert.DoesNotThrow(() => _web.Expect("web-card").HasColor("border-top-color", "#FF0000"));
    }

    [Test]
    public void A_wrong_web_colour_fails_with_both_values()
    {
        Element("web-xp", css: new() { ["color"] = "rgb(255, 0, 0)" });

        var message = FailureOf(() => _web.Expect("web-xp").HasTextColor("#7EB4F0"));

        Assert.That(message, Does.Contain("Expected 'web-xp' to have color #FF7EB4F0").And.Contain("#FFFF0000"));
    }

    [Test]
    public void ColorOf_parses_the_computed_colour()
    {
        Element("web-xp", css: new() { ["color"] = "rgb(126, 180, 240)", ["background-color"] = "rgba(10, 20, 30, 0.5)" });

        Assert.That(_web.ColorOf("web-xp"), Is.EqualTo(new HarnessColor(255, 126, 180, 240)));
        Assert.That(_web.ColorOf("web-xp", "background-color"), Is.EqualTo(new HarnessColor(128, 10, 20, 30)));
    }

    [Test]
    public void ColorOf_throws_for_something_that_is_not_a_colour()
    {
        Element("web-xp", css: new() { ["color"] = "" });

        Assert.Throws<FormatException>(() => _web.ColorOf("web-xp"));
    }

    #endregion

    #region Properties and attributes

    [Test]
    public void A_published_property_is_a_data_attribute()
    {
        Element("web-xp", attributes: new() { ["data-xp"] = "120" });

        Assert.DoesNotThrow(() => _web.Expect("web-xp").HasProperty("xp", "120").HasProperty("xp", 120));
        Assert.That(_web.PropertyOf("web-xp", "xp"), Is.EqualTo("120"));
    }

    [Test]
    public void A_property_falls_back_to_the_dom_property_of_that_name()
    {
        Element("web-name", properties: new() { ["value"] = "Ada" });

        Assert.DoesNotThrow(() => _web.Expect("web-name").HasProperty("value", "Ada"));
        Assert.That(_web.PropertyOf("web-name", "value"), Is.EqualTo("Ada"));
    }

    [Test]
    public void An_unpublished_property_reads_null_and_fails_clearly()
    {
        Element("web-xp");

        Assert.That(_web.PropertyOf("web-xp", "nope"), Is.Null);
        Assert.That(FailureOf(() => _web.Expect("web-xp").HasProperty("nope", "x")), Does.Contain("<not published>"));
    }

    [Test]
    public void Attributes_prefer_the_dom_attribute_then_the_dom_property()
    {
        Element("web-link", attributes: new() { ["aria-label"] = "Open" }, properties: new() { ["href"] = "http://x/y" });

        Assert.DoesNotThrow(() => _web.Expect("web-link").HasAttribute("aria-label", "Open").HasAttribute("href", "http://x/y"));
        Assert.That(_web.AttributeOf("web-link", "aria-label"), Is.EqualTo("Open"));
        Assert.That(_web.AttributeOf("web-link", "href"), Is.EqualTo("http://x/y"));
        Assert.That(_web.AttributeOf("web-link", "nope"), Is.Null);
    }

    #endregion

    #region Counting

    [Test]
    public void Count_counts_elements_matching_a_css_selector()
    {
        Select(".lesson-row", 4);

        Assert.That(_web.Count(".lesson-row"), Is.EqualTo(4));
        Assert.That(_web.Count(".nothing"), Is.Zero);
    }

    [Test]
    public void CountTestIds_matches_by_prefix()
    {
        Select("data-testid^='web-lesson-row-'", 3);

        Assert.That(_web.CountTestIds("web-lesson-row-"), Is.EqualTo(3));
    }

    [Test]
    public void CountTestIds_escapes_a_quote_in_the_prefix()
    {
        _web.CountTestIds("it's");

        _driver.Received().FindElements(Arg.Is<By>(b => b.ToString()!.Contains("it\\'s")));
    }

    [Test]
    public void ExpectCount_asserts_a_css_selector_count()
    {
        Select(".lesson-row", 3);

        Assert.DoesNotThrow(() => _web.ExpectCount(".lesson-row").Is(3));
        var message = FailureOf(() => _web.ExpectCount(".lesson-row").Is(5));
        Assert.That(message, Does.Contain("count of '.lesson-row' to be exactly 5").And.Contain("it was 3").And.Contain("Route:"));
    }

    [Test]
    public void ExpectCountWithPrefix_asserts_by_test_id_prefix()
    {
        Select("data-testid^='web-result-'", 2);

        Assert.DoesNotThrow(() => _web.ExpectCountWithPrefix("web-result-").Is(2));
        Assert.DoesNotThrow(() => _web.ExpectCountWithPrefix("web-result-").AtLeast(1));
        Assert.That(FailureOf(() => _web.ExpectCountWithPrefix("web-result-").IsEmpty()),
            Does.Contain("test ids starting 'web-result-'"));
    }

    [Test]
    public void ExpectCount_waits_for_the_list_to_fill()
    {
        var reads = 0;
        _driver.FindElements(Arg.Is<By>(b => b.ToString()!.Contains(".rows"))).Returns(_ =>
            new ReadOnlyCollection<IWebElement>(Enumerable.Range(0, ++reads < 4 ? 0 : 3).Select(_ => Substitute.For<IWebElement>()).ToList()));

        Assert.DoesNotThrow(() => _web.ExpectCount(".rows").WithTimeout(TimeSpan.FromSeconds(2)).Is(3));
    }

    #endregion

    [Test]
    public void Arguments_are_validated()
    {
        Assert.Throws<ArgumentException>(() => _web.Expect(" "));
        Assert.Throws<ArgumentException>(() => _web.ExpectCount(""));
        Assert.Throws<ArgumentException>(() => _web.ExpectCountWithPrefix(""));
        Assert.Throws<ArgumentException>(() => _web.Count(" "));
        Assert.Throws<ArgumentException>(() => _web.CountTestIds(""));
    }

    [Test]
    public void Expectations_log_through_the_web_ui_logger()
    {
        Element("web-xp", text: "1");

        _web.Expect("web-xp").HasText("1");

        Assert.That(_log, Has.Some.Contain("[expect]").And.Contain("✓"));
    }

    [Test]
    public void A_failure_attempts_a_screenshot_through_the_driver()
    {
        Element("web-xp", text: "0");
        var shooter = (ITakesScreenshot)_driver;
        shooter.GetScreenshot().Throws(new WebDriverException("no screenshot in this context"));

        var message = FailureOf(() => _web.Expect("web-xp").HasText("1"));

        Assert.That(message, Does.Contain("Expected 'web-xp' to have text '1'"));
        shooter.Received().GetScreenshot();
    }
}
