namespace Appium.Harness.Tests;

[TestFixture]
public class HarnessAssertionExceptionTests
{
    [Test]
    public void Carries_its_message()
    {
        Assert.That(new HarnessAssertionException("nope").Message, Is.EqualTo("nope"));
    }

    [Test]
    public void Can_wrap_the_exception_that_caused_it()
    {
        var inner = new InvalidOperationException("root cause");

        var ex = new HarnessAssertionException("outer", inner);

        Assert.That(ex.InnerException, Is.SameAs(inner));
        Assert.That(ex.Message, Is.EqualTo("outer"));
    }
}
