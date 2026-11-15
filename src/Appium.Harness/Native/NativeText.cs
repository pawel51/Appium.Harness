using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// An element's text, as the user reads it — with any probe payload taken out.
///
/// <para>XCUITest reports an element's text as its accessibility <i>value</i> when it has one, and
/// the app's <c>TestProbe</c> publishes through exactly that field on iOS. Without this a probed
/// label would read as <c>[[probe:…]]</c> and every <c>TextOf</c> on it — including the
/// page-object readers that parse a number out of it — would break. The real text is still there,
/// in the <c>label</c> attribute, so that is where this goes when the payload was all there was.
/// Text with no payload (every unprobed element, and all of Android) is returned untouched.</para>
/// </summary>
internal static class NativeText
{
    public static string Of(IWebElement element)
    {
        var text = element.Text;
        if (text is null || !text.Contains("[[probe:", StringComparison.Ordinal))
        {
            return text ?? string.Empty;
        }

        var stripped = ProbePayload.Strip(text);
        return stripped.Length > 0 ? stripped : ProbePayload.Strip(element.GetAttribute("label"));
    }
}
