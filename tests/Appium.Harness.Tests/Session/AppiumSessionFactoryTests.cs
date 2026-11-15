namespace Appium.Harness.Tests;

/// <summary>What a session asks for — checked without an Appium server.</summary>
[TestFixture]
public class AppiumSessionFactoryTests
{
    [Test]
    public void Android_defaults_to_UIAutomator2_and_grants_permissions()
    {
        var caps = AppiumSessionFactory.BuildCapabilities(new AppiumHarnessOptions { Platform = MobilePlatform.Android }).ToCapabilities();

        Assert.Multiple(() =>
        {
            Assert.That(caps.GetCapability("platformName"), Is.EqualTo("Android"));
            Assert.That(caps.GetCapability("appium:automationName"), Is.EqualTo("UIAutomator2"));
            Assert.That(caps.GetCapability("appium:autoGrantPermissions"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void IOS_defaults_to_XCUITest_and_accepts_alerts()
    {
        var caps = AppiumSessionFactory.BuildCapabilities(new AppiumHarnessOptions { Platform = MobilePlatform.iOS }).ToCapabilities();

        Assert.Multiple(() =>
        {
            Assert.That(caps.GetCapability("platformName"), Is.EqualTo("iOS"));
            Assert.That(caps.GetCapability("appium:automationName"), Is.EqualTo("XCUITest"));
            Assert.That(caps.GetCapability("appium:autoAcceptAlerts"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void A_cloud_run_sets_no_app_or_device_so_the_grid_config_wins()
    {
        var caps = AppiumSessionFactory.BuildCapabilities(new AppiumHarnessOptions
        {
            DeviceName = "Pixel 7",
            PlatformVersion = "14",
        }).ToCapabilities();

        Assert.Multiple(() =>
        {
            Assert.That(caps.HasCapability("appium:app"), Is.False);
            Assert.That(caps.HasCapability("appium:deviceName"), Is.False);
            Assert.That(caps.HasCapability("appium:platformVersion"), Is.False);
        });
    }

    [Test]
    public void A_local_run_sets_the_app_device_and_version()
    {
        var app = Path.GetTempFileName();
        try
        {
            var caps = AppiumSessionFactory.BuildCapabilities(new AppiumHarnessOptions
            {
                AppPath = app,
                DeviceName = "Pixel 7",
                PlatformVersion = "14",
            }).ToCapabilities();

            Assert.Multiple(() =>
            {
                Assert.That(caps.GetCapability("appium:app"), Is.EqualTo(app));
                Assert.That(caps.GetCapability("appium:deviceName"), Is.EqualTo("Pixel 7"));
                Assert.That(caps.GetCapability("appium:platformVersion"), Is.EqualTo("14"));
            });
        }
        finally
        {
            File.Delete(app);
        }
    }

    [Test]
    public void A_local_run_with_a_missing_app_fails_before_contacting_the_server()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"no-such-app-{Guid.NewGuid():N}.apk");

        var ex = Assert.Throws<FileNotFoundException>(() =>
            AppiumSessionFactory.BuildCapabilities(new AppiumHarnessOptions { AppPath = missing }));

        Assert.That(ex!.Message, Does.Contain(Directory.GetCurrentDirectory()));
    }

    [Test]
    public void Additional_capabilities_are_merged_verbatim()
    {
        var caps = AppiumSessionFactory.BuildCapabilities(new AppiumHarnessOptions
        {
            AdditionalCapabilities = new Dictionary<string, object> { ["chromedriverAutodownload"] = true },
        }).ToCapabilities();

        Assert.That(caps.GetCapability("appium:chromedriverAutodownload"), Is.EqualTo(true));
    }
}
