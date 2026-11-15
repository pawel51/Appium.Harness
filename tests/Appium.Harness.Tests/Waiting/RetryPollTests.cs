using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class RetryPollTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(5);

    [Test]
    public void Returns_the_first_non_null_value()
    {
        var reads = 0;

        var value = Retry.Poll(() => ++reads < 3 ? null : "found", TimeSpan.FromSeconds(5), Poll);

        Assert.That(value, Is.EqualTo("found"));
        Assert.That(reads, Is.EqualTo(3));
    }

    [Test]
    public void Returns_null_when_nothing_turns_up_in_time()
    {
        Assert.That(Retry.Poll<string>(() => null, TimeSpan.FromMilliseconds(30), Poll), Is.Null);
    }

    [Test]
    public void A_zero_timeout_still_looks_once()
    {
        var reads = 0;

        var value = Retry.Poll(() => { reads++; return "here"; }, TimeSpan.Zero, Poll);

        Assert.That(value, Is.EqualTo("here"));
        Assert.That(reads, Is.EqualTo(1));
    }

    [Test]
    public void Treats_a_driver_exception_as_not_yet()
    {
        var reads = 0;

        var value = Retry.Poll(
            () => ++reads == 1 ? throw new NoSuchElementException("not in the tree") : "found",
            TimeSpan.FromSeconds(5),
            Poll);

        Assert.That(value, Is.EqualTo("found"));
    }

    [Test]
    public void Lets_any_other_exception_through()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Retry.Poll<string>(() => throw new InvalidOperationException("a bug"), TimeSpan.FromSeconds(5), Poll));
    }
}
