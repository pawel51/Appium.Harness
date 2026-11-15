namespace Appium.Harness.Tests;

/// <summary>Which context <see cref="WebViewContext.Enter"/> switches into when several are offered.</summary>
[TestFixture]
public class WebViewContextTests
{
    [Test]
    public void Prefers_the_context_named_after_the_app_package()
    {
        string[] contexts = ["NATIVE_APP", "WEBVIEW_chrome", "WEBVIEW_com.example.app"];

        Assert.That(WebViewContext.Choose(contexts, "com.example.app"), Is.EqualTo("WEBVIEW_com.example.app"));
    }

    [Test]
    public void Falls_back_to_the_first_webview_without_a_hint()
    {
        string[] contexts = ["NATIVE_APP", "WEBVIEW_1", "WEBVIEW_2"];

        Assert.That(WebViewContext.Choose(contexts, null), Is.EqualTo("WEBVIEW_1"));
    }

    [Test]
    public void Falls_back_to_the_first_webview_when_the_hint_matches_none()
    {
        string[] contexts = ["NATIVE_APP", "WEBVIEW_1"];

        Assert.That(WebViewContext.Choose(contexts, "com.example.app"), Is.EqualTo("WEBVIEW_1"));
    }

    [Test]
    public void Returns_null_while_only_the_native_context_exists()
    {
        Assert.That(WebViewContext.Choose(["NATIVE_APP"], "com.example.app"), Is.Null);
    }
}
