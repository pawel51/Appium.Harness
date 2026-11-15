namespace Appium.Harness;

/// <summary>
/// Collects the ids of a list that does not fit on one screen.
///
/// <para>Both platforms virtualise: Android's accessibility tree holds only the rows currently
/// rendered, and iOS prunes off-screen cells, so counting what is in the tree undercounts any list
/// longer than the viewport. This scrolls to the top, then walks down, taking the union of what it
/// saw. The walk ends when a swipe changes nothing, so it is bounded by the content, not by a guess.</para>
/// </summary>
internal static class ScrollingCollector
{
    /// <param name="readIds">The matching ids currently in the tree.</param>
    /// <param name="fingerprint">Cheap summary of the whole screen (page source); equal before and after a swipe means the content did not move.</param>
    /// <param name="forward">Scroll toward the end of the list.</param>
    /// <param name="backward">Scroll toward the start of the list.</param>
    /// <param name="maxScrolls">Safety cap per direction, for a list that never stops changing (a live ticker, an animation).</param>
    public static IReadOnlySet<string> Collect(
        Func<IEnumerable<string>> readIds,
        Func<string> fingerprint,
        Action forward,
        Action backward,
        int maxScrolls = 15)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxScrolls);

        // A cap of 0 means "read what is on screen, do not scroll".
        if (maxScrolls == 0)
        {
            return new HashSet<string>(readIds(), StringComparer.Ordinal);
        }

        // Rewind first, so the count does not depend on where the previous step left the list -
        // and so that a retry, which collects again, sees the same list the first attempt did.
        if (!Settle(fingerprint, backward, maxScrolls, onMoved: null))
        {
            throw NotSettled("rewind to the start of the list", maxScrolls);
        }

        var seen = new HashSet<string>(readIds(), StringComparer.Ordinal);
        if (!Settle(fingerprint, forward, maxScrolls, onMoved: () => seen.UnionWith(readIds())))
        {
            throw NotSettled("reach the end of the list", maxScrolls);
        }

        return seen;
    }

    /// <summary>True once a swipe changed nothing; false if the cap ran out first.</summary>
    private static bool Settle(Func<string> fingerprint, Action scroll, int maxScrolls, Action? onMoved)
    {
        for (var i = 0; i < maxScrolls; i++)
        {
            var before = fingerprint();
            scroll();
            var after = fingerprint();

            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                return true;
            }

            onMoved?.Invoke();
        }

        return false;
    }

    // Not a WebDriverException on purpose: Retry polls through those, and a count that cannot be
    // trusted must stop the test loudly rather than undercount and look like a real mismatch.
    private static InvalidOperationException NotSettled(string what, int maxScrolls) =>
        new($"The list did not settle: {maxScrolls} swipes were not enough to {what}. " +
            "Raise maxScrolls, or check for content that never stops changing (a live ticker, an animation).");
}
