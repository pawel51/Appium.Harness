using System.Globalization;
using System.Text.RegularExpressions;

namespace Appium.Harness;

/// <summary>
/// Assertions about one element, that <b>wait for the condition to hold</b> rather than sampling
/// once. Reach one with <c>ui.Expect(id)</c> (native) or <c>web.Expect(testId)</c> (webview).
///
/// <para>A UI settles asynchronously, so "read it now and compare" is a race; every method here
/// polls until the expectation holds or the timeout passes, then throws a
/// <see cref="HarnessAssertionException"/> naming what it expected, what it last saw, and what
/// was on screen.</para>
///
/// <para>Methods return the expectation, so several checks on one element chain:
/// <c>ui.Expect(id).IsVisible().HasText("42").HasTextColor("#7EB4F0")</c>. <see cref="Not"/>
/// negates the <i>next</i> check only: <c>Expect(id).Not.IsEnabled().Not.IsToggledOn()</c> reads
/// as written. An element that is <i>absent</i>, or a colour or property that was <i>never
/// published</i>, satisfies neither polarity
/// of a text, colour, attribute or property check — there is nothing to assert about — and only
/// <see cref="IsVisible"/> treats absence as an answer.</para>
/// </summary>
public sealed class ElementExpectation
{
    private readonly IElementProbe _probe;
    private readonly ExpectSettings _settings;
    private readonly bool _negated;

    internal ElementExpectation(IElementProbe probe, ExpectSettings settings, bool negated = false)
    {
        _probe = probe;
        _settings = settings;
        _negated = negated;
    }

    /// <summary>
    /// Inverts the next check: <c>Expect(id).Not.IsVisible()</c> waits for the element to go away.
    /// The check that follows returns a positive expectation again, so chained checks never inherit it.
    /// </summary>
    public ElementExpectation Not => new(_probe, _settings, !_negated);

    /// <summary>The same expectation with a different wait, for one slow or one instant check.</summary>
    public ElementExpectation WithTimeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        return new ElementExpectation(_probe, _settings with { Timeout = timeout }, _negated);
    }

    // ---- State ---------------------------------------------------------------------------------

    /// <summary>The element is present and displayed. With <see cref="Not"/>: absent or hidden.</summary>
    public ElementExpectation IsVisible() =>
        Check("be visible", _probe.IsVisible, visible => visible, visible => visible ? "visible" : "not visible");

    /// <summary>The element is enabled.</summary>
    public ElementExpectation IsEnabled() =>
        Check("be enabled", _probe.ReadEnabled, enabled => enabled, enabled => enabled ? "enabled" : "disabled");

    /// <summary>A switch, checkbox or toggle is on.</summary>
    public ElementExpectation IsToggledOn() =>
        Check("be toggled on", _probe.ReadToggled, on => on, on => on ? "on" : "off");

    // ---- Text ----------------------------------------------------------------------------------

    /// <summary>The element's text equals <paramref name="expected"/>.</summary>
    /// <param name="expected">The text to match.</param>
    /// <param name="comparison">Case sensitivity. Ordinal by default, matching <c>UiDriver.WaitUntilText</c>.</param>
    /// <param name="trim">Trim whitespace from the actual text first — rendered text often carries padding.</param>
    public ElementExpectation HasText(string expected, StringComparison comparison = StringComparison.Ordinal, bool trim = false)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return Check(
            $"have text {Quote(expected)}",
            () => Normalise(_probe.ReadText(), trim),
            actual => string.Equals(actual, expected, comparison),
            Show);
    }

    /// <summary>The element's text contains <paramref name="fragment"/>.</summary>
    public ElementExpectation ContainsText(string fragment, StringComparison comparison = StringComparison.Ordinal)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        return Check(
            $"contain text {Quote(fragment)}",
            _probe.ReadText,
            actual => actual is not null && actual.Contains(fragment, comparison),
            Show);
    }

    /// <summary>The element's text matches <paramref name="pattern"/> (found anywhere — anchor it for a whole-text match).</summary>
    public ElementExpectation MatchesText(string pattern, RegexOptions options = RegexOptions.None)
    {
        var regex = new Regex(pattern, options, TimeSpan.FromSeconds(1));
        return Check(
            $"have text matching /{pattern}/",
            _probe.ReadText,
            actual => actual is not null && regex.IsMatch(actual),
            Show);
    }

    // ---- Colour --------------------------------------------------------------------------------

    /// <summary>The text colour is <paramref name="expected"/>, within <paramref name="tolerance"/> per channel.</summary>
    /// <param name="expected">A colour, or any string <see cref="HarnessColor.Parse"/> accepts.</param>
    /// <param name="tolerance">Allowed difference per 8-bit channel; 0 means exact.</param>
    public ElementExpectation HasTextColor(HarnessColor expected, int tolerance = 0) =>
        HasColor(_probe.TextColorName, expected, tolerance);

    /// <summary>The background colour is <paramref name="expected"/>, within <paramref name="tolerance"/> per channel.</summary>
    public ElementExpectation HasBackgroundColor(HarnessColor expected, int tolerance = 0) =>
        HasColor(_probe.BackgroundColorName, expected, tolerance);

    /// <summary>
    /// A named colour property is <paramref name="expected"/>. The name is the platform's own:
    /// a published MAUI property natively (<c>"Stroke"</c>), a CSS property on the web
    /// (<c>"border-color"</c>).
    /// </summary>
    public ElementExpectation HasColor(string propertyName, HarnessColor expected, int tolerance = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var suffix = tolerance == 0 ? string.Empty : $" (±{tolerance})";
        return Check(
            $"have {propertyName} {expected}{suffix}",
            () => _probe.ReadColor(propertyName),
            raw => HarnessColor.TryParse(raw, out var actual) && actual.IsCloseTo(expected, tolerance),
            raw => raw is null
                ? "<not published>"
                : HarnessColor.TryParse(raw, out var actual) ? $"{actual} (read as '{raw}')" : $"'{raw}' (not a colour)",
            available: raw => raw is not null);
    }

    // ---- Attributes and published properties ------------------------------------------------

    /// <summary>A raw platform attribute equals <paramref name="expected"/> (Android <c>content-desc</c>, iOS <c>value</c>, a DOM attribute…).</summary>
    public ElementExpectation HasAttribute(string name, string? expected, StringComparison comparison = StringComparison.Ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Check(
            $"have attribute '{name}' = {Quote(expected)}",
            () => _probe.ReadAttribute(name),
            actual => string.Equals(actual, expected, comparison),
            Show);
    }

    /// <summary>
    /// A property the app published equals <paramref name="expected"/> — a MAUI property or view-model
    /// value exposed through <c>TestProbe</c> natively, a <c>data-*</c> attribute on the web.
    /// </summary>
    public ElementExpectation HasProperty(string name, string expected, StringComparison comparison = StringComparison.Ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(expected);
        return Check(
            $"have property '{name}' = {Quote(expected)}",
            () => _probe.ReadProperty(name),
            actual => string.Equals(actual, expected, comparison),
            raw => raw is null ? "<not published>" : Quote(raw),
            available: raw => raw is not null);
    }

    /// <summary>A property the app published satisfies <paramref name="predicate"/>.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="predicate">Receives the published value, or null when it is not published.</param>
    /// <param name="description">What the predicate checks, for the failure message — e.g. <c>"be greater than 10"</c>.</param>
    public ElementExpectation HasProperty(string name, Func<string?, bool> predicate, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return Check(
            $"have property '{name}' that satisfies: {description}",
            () => _probe.ReadProperty(name),
            predicate,
            raw => raw is null ? "<not published>" : Quote(raw));
    }

    /// <summary>A published property parses as an integer and equals <paramref name="expected"/>.</summary>
    public ElementExpectation HasProperty(string name, int expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Check(
            $"have property '{name}' = {expected}",
            () => _probe.ReadProperty(name),
            raw => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actual) && actual == expected,
            raw => raw is null ? "<not published>" : Quote(raw),
            available: raw => raw is not null);
    }

    // ---- Engine --------------------------------------------------------------------------------

    private ElementExpectation Check<T>(
        string expectation,
        Func<T> read,
        Func<T, bool> pass,
        Func<T, string> show,
        Func<T, bool>? available = null)
    {
        // 'available' separates "there is a value to judge" from "the judgement": a colour or
        // property the app never published has nothing to compare, so it satisfies neither
        // polarity. Without this, Not.HasTextColor(...) would pass on a typo'd property name, or
        // on a build that never enabled TestProbe - the silent pass a negation invites.
        var result = Retry.Until(
            read,
            value => (available?.Invoke(value) ?? true) && pass(value) != _negated,
            _settings.Timeout,
            _settings.PollInterval);

        // An element that cannot be read never satisfies either polarity (Retry only accepts a
        // value it actually read). IsVisible is the exception by construction: its reader folds
        // absence into 'false' instead of throwing, so it is the one check that can see it.

        var verb = _negated ? "not to" : "to";
        if (result.Succeeded)
        {
            _settings.Log?.Invoke($"[expect] {_probe.Subject} {verb} {expectation} ✓ ({result.Elapsed.TotalSeconds:0.##}s)");
            return _negated ? new ElementExpectation(_probe, _settings) : this;
        }

        var observed = result.LastError is not null
            ? $"the element could not be read ({result.LastError})"
            : result.HasValue ? $"it was {show(result.LastValue!)}" : "nothing could be read";

        throw Fail($"Expected {_probe.Subject} {verb} {expectation}, but {observed}.", result.Attempts, result.Elapsed);
    }

    private HarnessAssertionException Fail(string headline, int attempts, TimeSpan elapsed)
    {
        var message = Failure.Describe(headline, attempts, elapsed, _probe.Diagnostics(), _settings, _probe.Subject);
        _settings.Log?.Invoke($"[expect] ✗ {headline}");
        return new HarnessAssertionException(message);
    }

    private static string? Normalise(string? text, bool trim) => trim ? text?.Trim() : text;

    private static string Show(string? value) => value is null ? "<null>" : Quote(value);

    private static string Quote(string? value) => value is null ? "<null>" : $"'{value}'";
}
