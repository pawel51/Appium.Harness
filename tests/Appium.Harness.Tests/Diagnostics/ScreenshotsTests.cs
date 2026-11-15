using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class ScreenshotsTests
{
    [Test]
    public void SanitiseFileName_replaces_characters_a_file_name_cannot_hold()
    {
        Assert.That(Screenshots.SanitiseFileName("expect-failed-'a/b'\0"), Is.EqualTo("expect-failed-'a_b'_"));
    }

    [Test]
    public void A_driver_that_cannot_take_screenshots_yields_null()
    {
        using var driver = Substitute.For<IWebDriver>();

        Assert.That(Screenshots.TryCapture(driver, Path.GetTempPath(), "x", null, "[t]"), Is.Null);
    }

    [Test]
    public void A_failed_capture_is_logged_and_yields_null_rather_than_throwing()
    {
        using var driver = Substitute.For<IWebDriver, ITakesScreenshot>();
        ((ITakesScreenshot)driver).GetScreenshot().Throws(new WebDriverException("session gone"));
        var log = new List<string>();

        var path = Screenshots.TryCapture(driver, Path.GetTempPath(), "shot", log.Add, "[t]");

        Assert.That(path, Is.Null);
        Assert.That(log, Has.One.Contains("[t] screenshot 'shot' failed: session gone"));
    }

    [Test]
    public void The_default_folder_is_under_the_OS_temp_folder()
    {
        Assert.That(AppiumHarnessOptions.DefaultFailureScreenshotDirectory, Does.StartWith(Path.GetTempPath()));
    }
}
