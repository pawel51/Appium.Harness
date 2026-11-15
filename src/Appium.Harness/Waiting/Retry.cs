using System.Diagnostics;

using OpenQA.Selenium;

namespace Appium.Harness;

/// <summary>What a polling run saw: whether it succeeded, and the last thing it observed.</summary>
internal readonly record struct RetryResult<T>(
    bool Succeeded,
    T? LastValue,
    bool HasValue,
    string? LastError,
    int Attempts,
    TimeSpan Elapsed);

/// <summary>
/// The polling primitive behind every expectation: read, test, and try again until the
/// condition holds or the time is up — then report <i>what it last saw</i>, which is the
/// difference between a failure message you can act on and "timed out".
/// </summary>
internal static class Retry
{
    /// <summary>
    /// Polls <paramref name="read"/> until <paramref name="accept"/> is satisfied.
    ///
    /// <para>Always reads at least once, so a zero timeout is a single check. A
    /// <see cref="WebDriverException"/> from the read — stale element, element not in the tree
    /// yet, tree mid-update — counts as "not yet" and is remembered; anything else is a bug in the
    /// test or the harness and propagates.</para>
    /// </summary>
    public static RetryResult<T> Until<T>(Func<T> read, Func<T, bool> accept, TimeSpan timeout, TimeSpan poll)
    {
        var clock = Stopwatch.StartNew();
        var attempts = 0;
        T? last = default;
        var hasValue = false;
        string? error = null;

        while (true)
        {
            attempts++;
            try
            {
                var value = read();
                last = value;
                hasValue = true;
                error = null;

                if (accept(value))
                {
                    return new RetryResult<T>(true, last, true, null, attempts, clock.Elapsed);
                }
            }
            catch (WebDriverException ex)
            {
                error = FirstLine(ex.Message);
            }

            var remaining = timeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return new RetryResult<T>(false, last, hasValue, error, attempts, clock.Elapsed);
            }

            Thread.Sleep(remaining < poll ? remaining : poll);
        }
    }

    /// <summary>
    /// Polls <paramref name="read"/> until it returns something, and returns that — or null when
    /// the time runs out. The shape every <c>Wait…</c> helper needs: "give me the element once it
    /// is there". Same rules as <see cref="Until{T}"/>: at least one read, and a
    /// <see cref="WebDriverException"/> means "not yet".
    /// </summary>
    public static T? Poll<T>(Func<T?> read, TimeSpan timeout, TimeSpan poll) where T : class
    {
        var result = Until(read, value => value is not null, timeout, poll);
        return result.Succeeded ? result.LastValue : null;
    }

    private static string FirstLine(string message)
    {
        var end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}
