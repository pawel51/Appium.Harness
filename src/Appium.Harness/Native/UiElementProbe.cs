namespace Appium.Harness;

/// <summary>
/// Reads one native element for an <see cref="ElementExpectation"/>.
///
/// <para>Text, enabled and toggled come straight from the accessibility tree. A MAUI colour or
/// view-model property is not in the tree at all, so those are read from the
/// <see cref="ProbePayload"/> the app folded into an accessibility field — the one place a
/// native tree will carry arbitrary text without the app changing what it shows.</para>
/// </summary>
internal sealed class UiElementProbe(NativeReader reader, string automationId) : IElementProbe
{
    private INativeScreen Screen => reader.Screen;

    public string Subject => $"'{automationId}'";

    public string TextColorName => ProbeProperties.TextColor;

    public string BackgroundColorName => ProbeProperties.BackgroundColor;

    public bool IsVisible() => Screen.Exists(automationId);

    public string ReadText() => NativeText.Of(Screen.Find(automationId));

    public bool ReadEnabled() => NativeState.IsEnabled(Screen.Find(automationId));

    // Against a freshly found element on every read: the engine polls, and a handle from the
    // previous attempt may belong to a row the list has since recycled.
    public bool ReadToggled() => NativeState.IsToggledOn(Screen.Find(automationId), Screen.IsAndroid);

    public string? ReadAttribute(string name) => Screen.Find(automationId).GetAttribute(name);

    public string? ReadProperty(string name) => reader.ReadProperty(automationId, name);

    public string? ReadColor(string name) => ReadProperty(name);

    public string Diagnostics() =>
        $"Visible ids: {string.Join(", ", Screen.VisibleIds().Take(40))}";
}
