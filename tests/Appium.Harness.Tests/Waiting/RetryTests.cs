using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class RetryTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(5);

    [Test]
    public void Succeeds_on_the_first_read_without_waiting()
    {
        var result = Retry.Until(() => 7, v => v == 7, TimeSpan.FromSeconds(5), Poll);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Attempts, Is.EqualTo(1));
        Assert.That(result.LastValue, Is.EqualTo(7));
        Assert.That(result.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Keeps_polling_until_the_condition_holds()
    {
        var reads = 0;

        var result = Retry.Until(() => ++reads, v => v >= 4, TimeSpan.FromSeconds(5), Poll);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Attempts, Is.EqualTo(4));
        Assert.That(result.LastValue, Is.EqualTo(4));
    }

    [Test]
    public void On_timeout_reports_the_last_value_it_saw()
    {
        var reads = 0;

        var result = Retry.Until(() => ++reads, v => false, TimeSpan.FromMilliseconds(60), Poll);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.HasValue, Is.True);
        Assert.That(result.LastValue, Is.EqualTo(reads));
        Assert.That(result.Attempts, Is.EqualTo(reads).And.GreaterThan(1));
        Assert.That(result.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(55)));
    }

    [Test]
    public void A_zero_timeout_is_a_single_check()
    {
        var reads = 0;

        var result = Retry.Until(() => ++reads, v => false, TimeSpan.Zero, Poll);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(reads, Is.EqualTo(1));
    }

    [Test]
    public void A_zero_timeout_still_succeeds_if_the_first_read_is_good()
    {
        Assert.That(Retry.Until(() => true, v => v, TimeSpan.Zero, Poll).Succeeded, Is.True);
    }

    [Test]
    public void Webdriver_exceptions_mean_not_yet_and_are_remembered()
    {
        var reads = 0;

        var result = Retry.Until<int>(
            () => ++reads < 3 ? throw new NoSuchElementException("no such element: gone") : reads,
            v => true,
            TimeSpan.FromSeconds(5),
            Poll);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Attempts, Is.EqualTo(3));
        Assert.That(result.LastError, Is.Null, "a later good read clears the error");
    }

    [Test]
    public void A_persistent_exception_is_reported_as_the_last_error_with_only_its_first_line()
    {
        var result = Retry.Until<int>(
            () => throw new StaleElementReferenceException("stale element\nbuild info: lots\nof noise"),
            v => true,
            TimeSpan.FromMilliseconds(40),
            Poll);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.HasValue, Is.False);
        Assert.That(result.LastError, Is.EqualTo("stale element"));
        Assert.That(result.Attempts, Is.GreaterThan(1));
    }

    [Test]
    public void An_exception_after_a_value_keeps_both()
    {
        var reads = 0;

        var result = Retry.Until<int>(
            () => ++reads == 1 ? 5 : throw new WebDriverException("tree changed"),
            v => false,
            TimeSpan.FromMilliseconds(40),
            Poll);

        Assert.That(result.HasValue, Is.True);
        Assert.That(result.LastValue, Is.EqualTo(5));
        Assert.That(result.LastError, Is.EqualTo("tree changed"));
    }

    [Test]
    public void A_value_after_an_exception_clears_the_error_even_when_it_is_not_accepted()
    {
        var reads = 0;

        var result = Retry.Until<int>(
            () => ++reads == 1 ? throw new WebDriverException("early") : 9,
            v => false,
            TimeSpan.FromMilliseconds(40),
            Poll);

        Assert.That(result.LastError, Is.Null);
        Assert.That(result.LastValue, Is.EqualTo(9));
    }

    [Test]
    public void Other_exceptions_are_bugs_and_propagate()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Retry.Until<int>(() => throw new InvalidOperationException("bug"), v => true, TimeSpan.FromSeconds(1), Poll));
    }

    [Test]
    public void An_exception_from_the_accept_function_propagates()
    {
        Assert.Throws<FormatException>(() =>
            Retry.Until(() => 1, v => throw new FormatException("bad predicate"), TimeSpan.FromSeconds(1), Poll));
    }

    [Test]
    public void Never_sleeps_past_the_deadline()
    {
        var result = Retry.Until(() => 0, v => false, TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(30));

        Assert.That(result.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }
}
