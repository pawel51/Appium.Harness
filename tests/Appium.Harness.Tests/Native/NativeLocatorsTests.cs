namespace Appium.Harness.Tests;

[TestFixture]
public class NativeLocatorsTests
{
    [Test]
    public void Android_id_regex_absorbs_the_package_qualifier()
    {
        var by = NativeLocators.ByIdRegex(isAndroid: true, "row-.*");

        Assert.That(by.ToString(), Does.Contain("resourceIdMatches(\"(.*/)?row-.*\")"));
    }

    [Test]
    public void IOS_id_regex_matches_the_bare_name()
    {
        var by = NativeLocators.ByIdRegex(isAndroid: false, "row-.*");

        Assert.That(by.ToString(), Does.Contain("name MATCHES 'row-.*'"));
    }

    [Test]
    public void IOS_text_lookup_only_matches_visible_elements()
    {
        Assert.That(NativeLocators.ByText(isAndroid: false, "OK").ToString(), Does.Contain("name == 'OK' AND visible == 1"));
    }

    [TestCase(true, "resource-id")]
    [TestCase(false, "name")]
    public void IdAttribute(bool isAndroid, string expected)
    {
        Assert.That(NativeLocators.IdAttribute(isAndroid), Is.EqualTo(expected));
    }
}
