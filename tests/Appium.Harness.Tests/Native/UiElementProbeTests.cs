using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

/// <summary>
/// The native probe, and the expectation engine wired to it — the same chain
/// <c>UiDriver.Expect</c> builds, over a fake screen instead of a device.
/// </summary>
[TestFixture]
public class UiElementProbeTests
{
    private static IWebElement Element(string? text = "", params (string Attribute, string? Value)[] attributes)
    {
        var element = Substitute.For<IWebElement>();
        element.Text.Returns(text ?? "");
        element.GetAttribute(Arg.Any<string>()).Returns(call =>
            attributes.FirstOrDefault(a => a.Attribute == call.Arg<string>()).Value);
        return element;
    }

    private static (UiElementProbe Probe, INativeScreen Screen) Build(bool android, IWebElement element, string id = "xp")
    {
        var screen = Substitute.For<INativeScreen>();
        screen.IsAndroid.Returns(android);
        screen.Find(id).Returns(element);
        screen.Exists(id).Returns(true);
        screen.VisibleIds().Returns(["page-profile", "xp"]);
        return (new UiElementProbe(new NativeReader(screen), id), screen);
    }

    private static ElementExpectation Expect(UiElementProbe probe) => new(probe, Settings.Fast());

    [Test]
    public void It_names_the_element_and_the_colour_properties_in_MAUI_terms()
    {
        var (probe, _) = Build(true, Element());

        Assert.That(probe.Subject, Is.EqualTo("'xp'"));
        Assert.That(probe.TextColorName, Is.EqualTo("TextColor"));
        Assert.That(probe.BackgroundColorName, Is.EqualTo("BackgroundColor"));
    }

    [Test]
    public void Visibility_and_text_come_from_the_screen()
    {
        var (probe, screen) = Build(true, Element("120"));

        Assert.That(probe.ReadText(), Is.EqualTo("120"));
        Assert.That(probe.IsVisible(), Is.True);

        screen.Exists("xp").Returns(false);
        Assert.That(probe.IsVisible(), Is.False);
    }

    [TestCase("true", true)]
    [TestCase("True", true)]
    [TestCase("1", true)]
    [TestCase("false", false)]
    [TestCase("False", false)]
    [TestCase("0", false)]
    [TestCase(null, true)]
    public void Enabled_is_read_from_the_enabled_attribute_and_defaults_to_true_when_missing(string? raw, bool expected)
    {
        var (probe, _) = Build(true, Element("", ("enabled", raw)));

        Assert.That(probe.ReadEnabled(), Is.EqualTo(expected));
    }

    [TestCase(true, "checked", "true", true)]
    [TestCase(true, "checked", "false", false)]
    [TestCase(true, "value", "1", false)]
    [TestCase(false, "value", "1", true)]
    [TestCase(false, "value", "0", false)]
    [TestCase(false, "checked", "true", false)]
    public void Toggled_reads_the_attribute_each_platform_actually_uses(bool android, string attribute, string raw, bool expected)
    {
        var (probe, _) = Build(android, Element("", (attribute, raw)));

        Assert.That(probe.ReadToggled(), Is.EqualTo(expected));
    }

    [Test]
    public void Toggled_falls_back_to_the_text_when_the_attribute_is_missing()
    {
        var (probe, _) = Build(false, Element("1"));

        Assert.That(probe.ReadToggled(), Is.True);
    }

    [Test]
    public void Attributes_are_passed_straight_through()
    {
        var (probe, _) = Build(true, Element("", ("content-desc", "Edit")));

        Assert.That(probe.ReadAttribute("content-desc"), Is.EqualTo("Edit"));
        Assert.That(probe.ReadAttribute("nope"), Is.Null);
    }

    [Test]
    public void Diagnostics_list_the_visible_ids()
    {
        var (probe, _) = Build(true, Element());

        Assert.That(probe.Diagnostics(), Is.EqualTo("Visible ids: page-profile, xp"));
    }

    [Test]
    public void Diagnostics_are_capped_at_forty_ids()
    {
        var (probe, screen) = Build(true, Element());
        screen.VisibleIds().Returns(Enumerable.Range(0, 100).Select(i => $"id{i}").ToList());

        var listed = probe.Diagnostics().Split(", ");

        Assert.That(listed, Has.Length.EqualTo(40));
    }

    [Test]
    public void Reads_of_an_absent_element_throw_the_driver_error_the_engine_polls_through()
    {
        var (probe, screen) = Build(true, Element());
        screen.Find("xp").Throws(new NoSuchElementException("gone"));

        Assert.Throws<NoSuchElementException>(() => probe.ReadText());
        Assert.Throws<NoSuchElementException>(() => probe.ReadEnabled());
        Assert.Throws<NoSuchElementException>(() => probe.ReadToggled());
        Assert.Throws<NoSuchElementException>(() => probe.ReadAttribute("x"));
        Assert.Throws<NoSuchElementException>(() => probe.ReadProperty("x"));
    }

    [TestCase(null, true, true)]
    [TestCase("true", true, false)]
    [TestCase("false", false, false)]
    public void ParseTrue(string? raw, bool whenMissing, bool _)
    {
        var expected = raw switch { null => whenMissing, "true" => true, _ => false };

        Assert.That(NativeState.ParseTrue(raw, whenMissing), Is.EqualTo(expected));
    }

    #region Through the expectation engine

    [Test]
    public void Android_text_colour_and_a_bound_value_assert_end_to_end()
    {
        var (probe, _) = Build(true, Element("120", ("content-desc", "[[probe:TextColor=#FF7EB4F0;Value=120]]")));

        Assert.DoesNotThrow(() => Expect(probe).IsVisible().HasText("120").HasTextColor("#7EB4F0").HasProperty("Value", 120));
    }

    [Test]
    public void Ios_text_colour_and_a_bound_value_assert_end_to_end()
    {
        var (probe, _) = Build(false, Element("120", ("value", "[[probe:TextColor=#FF7EB4F0;BackgroundColor=#FF000000]]")));

        Assert.DoesNotThrow(() => Expect(probe).HasText("120").HasTextColor("#7EB4F0").HasBackgroundColor("#000"));
    }

    [Test]
    public void A_wrong_colour_fails_with_both_colours_and_the_screen()
    {
        var (probe, _) = Build(true, Element("120", ("content-desc", "[[probe:TextColor=#FFFF0000]]")));

        var message = Assert.Throws<HarnessAssertionException>(() => Expect(probe).HasTextColor("#7EB4F0"))!.Message;

        Assert.That(message, Does.Contain("Expected 'xp' to have TextColor #FF7EB4F0").And.Contain("#FFFF0000").And.Contain("Visible ids: page-profile, xp"));
    }

    [Test]
    public void A_colour_appearing_late_is_waited_for()
    {
        var reads = 0;
        var element = Substitute.For<IWebElement>();
        element.GetAttribute("content-desc").Returns(_ => ++reads < 4 ? "[[probe:TextColor=#FF000000]]" : "[[probe:TextColor=#FF7EB4F0]]");
        var (probe, _) = Build(true, element);

        Assert.DoesNotThrow(() => new ElementExpectation(probe, Settings.Fast(timeoutMs: 2000)).HasTextColor("#7EB4F0"));
    }

    [Test]
    public void A_disabled_unticked_element_asserts_its_negations()
    {
        var (probe, _) = Build(true, Element("Save", ("enabled", "false"), ("checked", "false")));

        Assert.DoesNotThrow(() => Expect(probe).Not.IsEnabled().Not.IsToggledOn());
    }

    #endregion
}
