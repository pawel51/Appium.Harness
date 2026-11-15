using System.Collections.ObjectModel;

using NSubstitute;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

/// <summary>The readiness contract is configurable, for an app that does not use the default attribute names.</summary>
[TestFixture]
public class WebUiOptionsTests
{
    private IWebDriver _driver = null!;

    [SetUp]
    public void SetUp()
    {
        _driver = Substitute.For<IWebDriver, IJavaScriptExecutor>();
        _driver.FindElements(Arg.Any<By>()).Returns(new ReadOnlyCollection<IWebElement>([]));
    }

    [TearDown]
    public void TearDown() => _driver.Dispose();

    private WebUi Web(WebUiOptions? options = null) => new(_driver, options ?? new WebUiOptions
    {
        DefaultTimeout = TimeSpan.FromMilliseconds(60),
        PollInterval = TimeSpan.FromMilliseconds(5),
    });

    private void Root(string attribute, string value)
    {
        var root = Substitute.For<IWebElement>();
        root.GetAttribute(attribute).Returns(value);
        _driver.FindElement(Arg.Is<By>(b => b.ToString()!.Contains($"[{attribute}]"))).Returns(root);
    }

    [Test]
    public void Defaults_are_the_shipped_conventions()
    {
        var options = new WebUiOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.TestIdAttribute, Is.EqualTo("data-testid"));
            Assert.That(options.AppStateAttribute, Is.EqualTo("data-app-state"));
            Assert.That(options.RouteAttribute, Is.EqualTo("data-route"));
            Assert.That(options.ReadyState, Is.EqualTo("ready"));
        });
    }

    [Test]
    public void ByTestId_uses_the_configured_attribute()
    {
        var web = Web(new WebUiOptions { TestIdAttribute = "data-qa" });

        Assert.That(web.ByTestId("save").ToString(), Does.Contain("[data-qa='save']"));
    }

    [Test]
    public void Readiness_uses_the_configured_attribute_and_value()
    {
        Root("data-status", "idle");
        Root("data-page", "home");
        var web = Web(new WebUiOptions
        {
            AppStateAttribute = "data-status",
            ReadyState = "idle",
            RouteAttribute = "data-page",
            DefaultTimeout = TimeSpan.FromMilliseconds(60),
            PollInterval = TimeSpan.FromMilliseconds(5),
        });

        Assert.Multiple(() =>
        {
            Assert.That(web.AppState, Is.EqualTo("idle"));
            Assert.That(web.WaitForAppReady(), Is.True);
            Assert.That(web.WaitForRoute("home"), Is.True);
            Assert.That(web.WaitForRoute("profile"), Is.False);
        });
    }

    [Test]
    public void The_default_ready_value_does_not_satisfy_a_custom_one()
    {
        Root("data-app-state", "ready");
        var web = Web(new WebUiOptions
        {
            ReadyState = "settled",
            DefaultTimeout = TimeSpan.FromMilliseconds(30),
            PollInterval = TimeSpan.FromMilliseconds(5),
        });

        Assert.That(web.WaitForAppReady(), Is.False);
    }

    [Test]
    public void RootAttribute_reads_app_published_state()
    {
        Root("data-user-id", "u-42");

        Assert.That(Web().RootAttribute("data-user-id"), Is.EqualTo("u-42"));
    }

    [Test]
    public void RootAttribute_is_null_when_nothing_carries_it()
    {
        _driver.FindElement(Arg.Any<By>()).Returns(_ => throw new NoSuchElementException("none"));

        Assert.That(Web().RootAttribute("data-user-id"), Is.Null);
    }
}
