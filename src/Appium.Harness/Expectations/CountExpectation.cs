namespace Appium.Harness;

/// <summary>
/// Assertions about how many elements match — the length of a list, the number of results.
/// Reach one with <c>ui.ExpectCount(idRegex)</c> or <c>web.ExpectCount(cssSelector)</c>.
///
/// <para>Like <see cref="ElementExpectation"/> it polls until the count satisfies the check, so
/// "the list shows 3 results" holds even when the results arrive a moment after the tap.</para>
/// </summary>
public sealed class CountExpectation
{
    private readonly string _subject;
    private readonly Func<int> _count;
    private readonly Func<string> _diagnostics;
    private readonly ExpectSettings _settings;
    private readonly bool _negated;

    internal CountExpectation(
        string subject,
        Func<int> count,
        Func<string> diagnostics,
        ExpectSettings settings,
        bool negated = false)
    {
        _subject = subject;
        _count = count;
        _diagnostics = diagnostics;
        _settings = settings;
        _negated = negated;
    }

    /// <summary>The same expectation, inverted: <c>ExpectCount(x).Not.Is(0)</c> means "at least one".</summary>
    public CountExpectation Not => new(_subject, _count, _diagnostics, _settings, !_negated);

    /// <summary>The same expectation with a different wait.</summary>
    public CountExpectation WithTimeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        return new CountExpectation(_subject, _count, _diagnostics, _settings with { Timeout = timeout }, _negated);
    }

    /// <summary>Exactly <paramref name="expected"/> elements match.</summary>
    public void Is(int expected) => Check($"be exactly {expected}", n => n == expected);

    /// <summary>At least <paramref name="minimum"/> elements match.</summary>
    public void AtLeast(int minimum) => Check($"be at least {minimum}", n => n >= minimum);

    /// <summary>At most <paramref name="maximum"/> elements match.</summary>
    public void AtMost(int maximum) => Check($"be at most {maximum}", n => n <= maximum);

    /// <summary>Between <paramref name="minimum"/> and <paramref name="maximum"/> elements match, inclusive.</summary>
    public void Between(int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            throw new ArgumentException($"maximum ({maximum}) is below minimum ({minimum}).", nameof(maximum));
        }

        Check($"be between {minimum} and {maximum}", n => n >= minimum && n <= maximum);
    }

    /// <summary>No element matches.</summary>
    public void IsEmpty() => Check("be zero", n => n == 0);

    private void Check(string expectation, Func<int, bool> pass)
    {
        var result = Retry.Until(_count, n => pass(n) != _negated, _settings.Timeout, _settings.PollInterval);

        var verb = _negated ? "not to" : "to";
        if (result.Succeeded)
        {
            _settings.Log?.Invoke($"[expect] count of {_subject} {verb} {expectation} ✓ ({result.LastValue})");
            return;
        }

        var observed = result.LastError is not null
            ? $"the elements could not be counted ({result.LastError})"
            : result.HasValue ? $"it was {result.LastValue}" : "nothing could be read";

        var headline = $"Expected the count of {_subject} {verb} {expectation}, but {observed}.";
        _settings.Log?.Invoke($"[expect] ✗ {headline}");

        throw new HarnessAssertionException(
            Failure.Describe(headline, result.Attempts, result.Elapsed, _diagnostics(), _settings, _subject));
    }
}
