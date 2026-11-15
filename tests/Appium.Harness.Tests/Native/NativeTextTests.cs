using NSubstitute;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class NativeTextTests
{
    private static IWebElement Element(string? text, string? label = null)
    {
        var element = Substitute.For<IWebElement>();
        element.Text.Returns(text!);
        element.GetAttribute("label").Returns(label);
        return element;
    }

    [Test]
    public void Text_without_a_payload_is_returned_untouched_and_the_label_is_not_consulted()
    {
        var element = Element("  120  ", label: "ignored");

        Assert.That(NativeText.Of(element), Is.EqualTo("  120  "));
        element.DidNotReceive().GetAttribute("label");
    }

    [Test]
    public void Text_that_is_only_the_payload_falls_back_to_the_label()
    {
        // iOS: XCUITest reports the accessibility value as the text, so a probed label's own text is in 'label'.
        Assert.That(NativeText.Of(Element("[[probe:Value=0]]", label: "0")), Is.EqualTo("0"));
    }

    [Test]
    public void Text_that_has_a_payload_and_more_keeps_the_more()
    {
        Assert.That(NativeText.Of(Element("120 [[probe:Value=120]]", label: "should not be needed")), Is.EqualTo("120"));
    }

    [Test]
    public void The_label_fallback_is_stripped_too()
    {
        Assert.That(NativeText.Of(Element("[[probe:a=1]]", label: "7 [[probe:a=1]]")), Is.EqualTo("7"));
    }

    [Test]
    public void A_payload_with_no_label_reads_empty_rather_than_leaking_the_payload()
    {
        Assert.That(NativeText.Of(Element("[[probe:a=1]]", label: null)), Is.Empty);
    }

    [Test]
    public void Null_text_reads_empty()
    {
        Assert.That(NativeText.Of(Element(null)), Is.Empty);
    }

    [Test]
    public void The_probe_reads_text_through_it()
    {
        var screen = Substitute.For<INativeScreen>();
        var element = Element("[[probe:Value=3]]", label: "3");
        screen.Find("xp").Returns(element);

        Assert.That(new UiElementProbe(new NativeReader(screen), "xp").ReadText(), Is.EqualTo("3"));
    }

    [Test]
    public void Toggle_text_fallback_also_ignores_the_payload()
    {
        var screen = Substitute.For<INativeScreen>();
        screen.IsAndroid.Returns(false);
        var element = Element("[[probe:a=1]]", label: "1");
        element.GetAttribute("value").Returns((string?)null);
        screen.Find("sw").Returns(element);

        Assert.That(new UiElementProbe(new NativeReader(screen), "sw").ReadToggled(), Is.True);
    }
}
