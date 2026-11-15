namespace Appium.Harness;

/// <summary>
/// Thrown when an <see cref="ElementExpectation"/> or <see cref="CountExpectation"/> is not met
/// within its timeout.
///
/// A plain <see cref="Exception"/> on purpose: the harness takes no dependency on a test
/// framework, and every runner (NUnit, xUnit, MSTest) reports an unhandled exception as a failed
/// test. The message carries the expected and last-observed values, what was on screen, and a
/// screenshot path when one could be captured.
/// </summary>
public sealed class HarnessAssertionException : Exception
{
    /// <summary>Creates the exception with a failure message.</summary>
    public HarnessAssertionException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a failure message and the exception that triggered it.</summary>
    public HarnessAssertionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
