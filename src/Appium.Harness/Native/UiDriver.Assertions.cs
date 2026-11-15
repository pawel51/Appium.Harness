using OpenQA.Selenium;

namespace Appium.Harness;

// Assertions and the readers behind them. A separate file because it is a separate concern: the
// rest of UiDriver acts on the app; this part states what the app should look like. The logic
// lives in NativeReader / ElementExpectation over INativeScreen, so it is tested without a device;
// what is left here is wiring.
public sealed partial class UiDriver
{
    private NativeReader? _reader;

    private NativeReader Reader => _reader ??= new NativeReader(new Screen(this));

    #region Expecting

    /// <summary>
    /// Assertions about one element that wait for the condition to hold, then throw a
    /// <see cref="HarnessAssertionException"/> saying what was expected and what was seen.
    ///
    /// <code>
    /// Ui.Expect(AutomationIds.ProfileTotalXp).HasText("120").HasTextColor("#7EB4F0");
    /// Ui.Expect(AutomationIds.ProfileEdit).Not.IsVisible();
    /// </code>
    /// </summary>
    public ElementExpectation Expect(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        return new ElementExpectation(new UiElementProbe(Reader, automationId), CreateExpectSettings());
    }

    /// <summary>
    /// Assertions about how many elements match an id pattern — the rows in a list.
    ///
    /// <code>
    /// Ui.ExpectCount("search-result-.*").Is(3);
    /// Ui.ExpectCount("lesson-row-.*", scroll: true).AtLeast(12);   // longer than one screen
    /// </code>
    /// </summary>
    /// <param name="idRegex">Matched against the whole simple id, as <see cref="AnyIdMatches"/> does.</param>
    /// <param name="scroll">Scroll the list and count every row, not just those rendered now — see <see cref="CountIds"/>.</param>
    /// <param name="maxScrolls">Per-direction cap on swipes when <paramref name="scroll"/> is set.</param>
    public CountExpectation ExpectCount(string idRegex, bool scroll = false, int maxScrolls = 15)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idRegex);
        return new CountExpectation(
            $"ids matching /{idRegex}/",
            () => Reader.CountIds(idRegex, scroll, maxScrolls),
            VisibleIdsDiagnostic,
            CreateExpectSettings());
    }

    private ExpectSettings CreateExpectSettings() => new(
        DefaultTimeout,
        _options.PollInterval,
        _options.Log,
        name => TryCaptureScreenshot(_options.FailureScreenshotDirectory ?? Screenshots.DefaultDirectory, name));

    #endregion

    #region Reading

    /// <summary>
    /// How many elements match an id pattern right now.
    ///
    /// <para>Counts what is <i>in the accessibility tree</i>, and both platforms virtualise: a list
    /// longer than the screen reports only the rows currently rendered. Pass
    /// <paramref name="scroll"/> to scroll to the top, walk to the end and count the distinct ids
    /// seen — which counts rows by id, so it needs a row id per item (an id builder
    /// such as <c>$"row-{item.Id}"</c> gives one) and leaves the list scrolled to its end.</para>
    ///
    /// <para>The two modes can differ on a screen with duplicate ids: without scrolling every matching
    /// <i>element</i> is counted, with it every distinct <i>id</i>. A container and its child that
    /// share an id count twice in the first and once in the second.</para>
    ///
    /// <para>With <paramref name="scroll"/>, a list that is still changing after
    /// <paramref name="maxScrolls"/> swipes throws <see cref="InvalidOperationException"/> instead
    /// of returning a count that cannot be trusted.</para>
    /// </summary>
    public int CountIds(string idRegex, bool scroll = false, int maxScrolls = 15)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idRegex);
        return Reader.CountIds(idRegex, scroll, maxScrolls);
    }

    /// <summary>
    /// A property the app published for an element through <c>TestProbe</c> — a MAUI property
    /// (<c>"TextColor"</c>) or a bound view-model value (<c>"Value"</c>). Null when it was not published.
    /// </summary>
    public string? PropertyOf(string automationId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Reader.ReadProperty(automationId, name);
    }

    /// <summary>Everything the app published for an element through <c>TestProbe</c>. Empty if nothing.</summary>
    public IReadOnlyDictionary<string, string> Properties(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        return Reader.ReadProperties(automationId);
    }

    /// <summary>
    /// A colour the app published for an element, parsed. Null when it was not published.
    /// Throws <see cref="FormatException"/> if what was published is not a colour.
    /// </summary>
    /// <param name="automationId">The element.</param>
    /// <param name="property">The MAUI property name; <c>"TextColor"</c> by default.</param>
    public HarnessColor? ColorOf(string automationId, string property = ProbeProperties.TextColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        return Reader.ReadColor(automationId, property);
    }

    #endregion

    /// <summary>The live-session half of <see cref="INativeScreen"/>.</summary>
    private sealed class Screen(UiDriver ui) : INativeScreen
    {
        public bool IsAndroid => ui.IsAndroid;

        public IWebElement Find(string automationId) => ui.Find(automationId);

        public bool Exists(string automationId) => ui.Exists(automationId);

        public IReadOnlyList<IWebElement> FindByIdRegex(string idRegex) => ui.FindByIdRegex(idRegex);

        public IReadOnlyList<string> VisibleIds() => ui.VisibleAutomationIds();

        public string PageSource() => ui.SafePageSource();

        public void SwipeUp() => ui.SwipeUp();

        public void SwipeDown() => ui.SwipeDown();
    }
}
