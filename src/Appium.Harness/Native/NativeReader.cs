using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>
/// Reads what the native accessibility tree carries — counts, and the properties the app folded
/// into an accessibility field with <see cref="ProbePayload"/>. All of it over
/// <see cref="INativeScreen"/>, so none of it needs a device to test.
/// </summary>
internal sealed class NativeReader(INativeScreen screen)
{
    public INativeScreen Screen { get; } = screen;

    /// <summary>
    /// Where a native tree carries the probe payload, in the order they are tried.
    /// Android's <c>content-desc</c> leaves the element's <c>text</c> untouched; on iOS the
    /// accessibility <c>value</c> leaves its <c>label</c> (the text) untouched.
    /// </summary>
    public string[] Carriers => Screen.IsAndroid ? ["content-desc"] : ["value", "label"];

    public int CountIds(string idRegex, bool scroll, int maxScrolls)
    {
        if (!scroll)
        {
            return Screen.FindByIdRegex(idRegex).Count;
        }

        return ScrollingCollector.Collect(
            readIds: () => MatchingIds(idRegex),
            fingerprint: Screen.PageSource,
            forward: Screen.SwipeUp,
            backward: Screen.SwipeDown,
            maxScrolls).Count;
    }

    public string? ReadProperty(string automationId, string name)
    {
        var element = Screen.Find(automationId);
        foreach (var carrier in Carriers)
        {
            if (ProbePayload.TryGet(element.GetAttribute(carrier), name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    public IReadOnlyDictionary<string, string> ReadProperties(string automationId)
    {
        var element = Screen.Find(automationId);
        return Carriers
            .Select(carrier => ProbePayload.Parse(element.GetAttribute(carrier)))
            .FirstOrDefault(payload => payload.Count > 0)
            ?? new Dictionary<string, string>();
    }

    public HarnessColor? ReadColor(string automationId, string property) =>
        ReadProperty(automationId, property) is { } raw ? HarnessColor.Parse(raw) : (HarnessColor?)null;

    private IEnumerable<string> MatchingIds(string idRegex)
    {
        var attribute = NativeLocators.IdAttribute(Screen.IsAndroid);
        foreach (var element in Screen.FindByIdRegex(idRegex))
        {
            string? id;
            try
            {
                id = element.GetAttribute(attribute);
            }
            catch (WebDriverException)
            {
                continue; // recycled between the query and the read — it is seen on the next pass
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                yield return NativeLocators.SimpleId(id);
            }
        }
    }
}
