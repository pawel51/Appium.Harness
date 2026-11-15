namespace Appium.Harness.Tests;

[TestFixture]
public class CountExpectationTests
{
    private static CountExpectation Count(Func<int> count, ExpectSettings? settings = null, Func<string>? diagnostics = null) =>
        new("ids matching /row-.*/", count, diagnostics ?? (() => "Visible ids: row-0, row-1"), settings ?? Settings.Fast());

    private static string FailureOf(TestDelegate act) =>
        Assert.Throws<HarnessAssertionException>(act)!.Message;

    [Test]
    public void Is_passes_on_the_exact_count()
    {
        Assert.DoesNotThrow(() => Count(() => 3).Is(3));
    }

    [Test]
    public void Is_waits_for_the_list_to_fill()
    {
        var count = FakeProbe.Eventually(badReads: 4, bad: 0, good: 3);

        Assert.DoesNotThrow(() => Count(count, Settings.Fast(timeoutMs: 2000)).Is(3));
    }

    [Test]
    public void Is_failure_names_the_expected_and_the_last_count_and_the_diagnostics()
    {
        var message = FailureOf(() => Count(() => 2).Is(3));

        Assert.That(message, Does.Contain("Expected the count of ids matching /row-.*/ to be exactly 3, but it was 2."));
        Assert.That(message, Does.Contain("Visible ids: row-0, row-1"));
    }

    [TestCase(5, 3, true)]
    [TestCase(3, 3, true)]
    [TestCase(2, 3, false)]
    public void AtLeast(int actual, int minimum, bool passes)
    {
        if (passes) Assert.DoesNotThrow(() => Count(() => actual).AtLeast(minimum));
        else Assert.That(FailureOf(() => Count(() => actual).AtLeast(minimum)), Does.Contain("to be at least 3"));
    }

    [TestCase(1, 3, true)]
    [TestCase(3, 3, true)]
    [TestCase(4, 3, false)]
    public void AtMost(int actual, int maximum, bool passes)
    {
        if (passes) Assert.DoesNotThrow(() => Count(() => actual).AtMost(maximum));
        else Assert.That(FailureOf(() => Count(() => actual).AtMost(maximum)), Does.Contain("to be at most 3"));
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    public void Between_is_inclusive(int actual, bool passes)
    {
        if (passes) Assert.DoesNotThrow(() => Count(() => actual).Between(2, 4));
        else Assert.That(FailureOf(() => Count(() => actual).Between(2, 4)), Does.Contain("to be between 2 and 4"));
    }

    [Test]
    public void Between_rejects_an_inverted_range()
    {
        Assert.Throws<ArgumentException>(() => Count(() => 1).Between(4, 2));
    }

    [Test]
    public void IsEmpty_passes_on_zero_and_fails_otherwise()
    {
        Assert.DoesNotThrow(() => Count(() => 0).IsEmpty());
        Assert.That(FailureOf(() => Count(() => 2).IsEmpty()), Does.Contain("to be zero").And.Contain("it was 2"));
    }

    [Test]
    public void Not_IsEmpty_means_at_least_one()
    {
        Assert.DoesNotThrow(() => Count(() => 1).Not.IsEmpty());
        Assert.That(FailureOf(() => Count(() => 0).Not.IsEmpty()), Does.Contain("not to be zero"));
    }

    [Test]
    public void Not_Is_passes_for_any_other_count_and_waits_for_it()
    {
        Assert.DoesNotThrow(() => Count(() => 2).Not.Is(3));
        Assert.DoesNotThrow(() => Count(FakeProbe.Eventually(badReads: 3, bad: 3, good: 4), Settings.Fast(timeoutMs: 2000)).Not.Is(3));
        Assert.That(FailureOf(() => Count(() => 3).Not.Is(3)), Does.Contain("not to be exactly 3"));
    }

    [Test]
    public void A_count_that_cannot_be_taken_fails_with_the_reason()
    {
        var message = FailureOf(() => Count(() => throw new OpenQA.Selenium.WebDriverException("session lost")).Is(1));

        Assert.That(message, Does.Contain("could not be counted").And.Contain("session lost"));
    }

    [Test]
    public void A_count_that_recovers_after_an_error_passes()
    {
        var reads = 0;

        Assert.DoesNotThrow(() => Count(
            () => ++reads < 3 ? throw new OpenQA.Selenium.WebDriverException("busy") : 5,
            Settings.Fast(timeoutMs: 2000)).Is(5));
    }

    [Test]
    public void WithTimeout_applies_and_keeps_negation()
    {
        var reads = 0;
        var expectation = Count(() => { reads++; return 1; }, Settings.Fast(timeoutMs: 5000));

        Assert.Throws<HarnessAssertionException>(() => expectation.WithTimeout(TimeSpan.Zero).Is(2));
        Assert.That(reads, Is.EqualTo(1));
        Assert.DoesNotThrow(() => expectation.Not.WithTimeout(TimeSpan.Zero).Is(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => expectation.WithTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Test]
    public void Results_are_logged_and_a_screenshot_is_attached_on_failure()
    {
        var log = new List<string>();
        var settings = Settings.Fast(log: log.Add, screenshot: _ => "/tmp/count.png");

        Count(() => 3, settings).Is(3);
        var message = FailureOf(() => Count(() => 1, settings).Is(3));

        Assert.That(log[0], Does.Contain("✓"));
        Assert.That(log[1], Does.Contain("✗"));
        Assert.That(message, Does.Contain("Screenshot: /tmp/count.png"));
    }
}
