using NSubstitute;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class NativeStateTests
{
    private static IWebElement Element(string? enabled = null, string? @checked = null, string? value = null, string text = "")
    {
        var element = Substitute.For<IWebElement>();
        element.GetAttribute("enabled").Returns(enabled);
        element.GetAttribute("checked").Returns(@checked);
        element.GetAttribute("value").Returns(value);
        element.Text.Returns(text);
        return element;
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    [TestCase(null, true)] // no flag is not a disabled control
    public void IsEnabled(string? raw, bool expected)
    {
        Assert.That(NativeState.IsEnabled(Element(enabled: raw)), Is.EqualTo(expected));
    }

    [Test]
    public void Android_reads_checked_and_ignores_value()
    {
        var element = Element(@checked: "true", value: "0");

        Assert.That(NativeState.IsToggledOn(element, isAndroid: true), Is.True);
    }

    [Test]
    public void IOS_reads_value_and_ignores_checked()
    {
        var element = Element(@checked: "true", value: "0");

        Assert.That(NativeState.IsToggledOn(element, isAndroid: false), Is.False);
    }

    [Test]
    public void Falls_back_to_the_text_when_the_platform_attribute_is_missing()
    {
        Assert.That(NativeState.IsToggledOn(Element(text: "1"), isAndroid: false), Is.True);
    }
}
