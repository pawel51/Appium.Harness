namespace Appium.Harness.Tests;

[TestFixture]
public class ScrollingCollectorTests
{
    /// <summary>A virtualised list: only a window of rows is "in the tree", and swiping moves the window.</summary>
    private sealed class VirtualList(int total, int window, int step, int start = 0)
    {
        public int Offset { get; private set; } = start;

        public int Forward { get; private set; }

        public int Backward { get; private set; }

        public IEnumerable<string> Visible() =>
            Enumerable.Range(Offset, Math.Min(window, total - Offset)).Select(i => $"row-{i}");

        public string Fingerprint() => $"offset={Offset}";

        public void SwipeUp()
        {
            Forward++;
            Offset = Math.Min(Offset + step, Math.Max(total - window, 0));
        }

        public void SwipeDown()
        {
            Backward++;
            Offset = Math.Max(Offset - step, 0);
        }
    }

    private static IReadOnlySet<string> Collect(VirtualList list, int maxScrolls = 15) =>
        ScrollingCollector.Collect(list.Visible, list.Fingerprint, list.SwipeUp, list.SwipeDown, maxScrolls);

    [Test]
    public void Counts_every_row_of_a_list_longer_than_the_screen()
    {
        var list = new VirtualList(total: 25, window: 6, step: 4);

        Assert.That(Collect(list), Has.Count.EqualTo(25));
    }

    [Test]
    public void Starting_mid_list_still_counts_the_whole_list()
    {
        var list = new VirtualList(total: 25, window: 6, step: 4, start: 12);

        Assert.That(Collect(list), Has.Count.EqualTo(25));
    }

    [Test]
    public void A_list_that_fits_on_one_screen_needs_no_scrolling_to_count()
    {
        var list = new VirtualList(total: 3, window: 6, step: 4);

        Assert.That(Collect(list), Has.Count.EqualTo(3));
    }

    [Test]
    public void An_empty_list_counts_zero()
    {
        Assert.That(Collect(new VirtualList(total: 0, window: 6, step: 4)), Is.Empty);
    }

    [Test]
    public void Rewinds_before_it_counts_forward()
    {
        var list = new VirtualList(total: 25, window: 6, step: 4, start: 20);

        Collect(list);

        Assert.That(list.Backward, Is.GreaterThan(0));
        Assert.That(list.Forward, Is.GreaterThan(0));
    }

    [Test]
    public void A_second_collection_gives_the_same_answer_as_the_first()
    {
        var list = new VirtualList(total: 25, window: 6, step: 4);

        var first = Collect(list).Count;
        var second = Collect(list).Count;

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Distinct_ids_are_counted_once_even_if_swipes_overlap()
    {
        var list = new VirtualList(total: 10, window: 6, step: 2);

        Assert.That(Collect(list), Has.Count.EqualTo(10));
    }

    [Test]
    public void A_list_that_never_settles_throws_instead_of_returning_a_count_it_cannot_vouch_for()
    {
        var swipes = 0;
        var seen = 0;

        var ex = Assert.Throws<InvalidOperationException>(() => ScrollingCollector.Collect(
            readIds: () => [$"row-{seen++}"],
            fingerprint: () => $"tick-{swipes}-{seen}",
            forward: () => swipes++,
            backward: () => swipes++,
            maxScrolls: 3));

        Assert.That(ex!.Message, Does.Contain("did not settle").And.Contain("3 swipes").And.Contain("maxScrolls"));
        Assert.That(swipes, Is.LessThanOrEqualTo(3), "it stopped at the cap, in the rewind");
    }

    [Test]
    public void A_list_longer_than_the_cap_can_walk_throws_at_the_end_of_the_walk()
    {
        // 100 rows, 6 visible, 4 per swipe: needs ~24 forward swipes, so a cap of 5 cannot reach the end.
        var list = new VirtualList(total: 100, window: 6, step: 4);

        var ex = Assert.Throws<InvalidOperationException>(() => Collect(list, maxScrolls: 5));

        Assert.That(ex!.Message, Does.Contain("reach the end of the list"));
    }

    [Test]
    public void A_list_starting_deep_in_throws_at_the_rewind_when_the_cap_cannot_reach_the_top()
    {
        var list = new VirtualList(total: 100, window: 6, step: 4, start: 90);

        var ex = Assert.Throws<InvalidOperationException>(() => Collect(list, maxScrolls: 5));

        Assert.That(ex!.Message, Does.Contain("rewind to the start of the list"));
    }

    [Test]
    public void A_cap_just_big_enough_succeeds()
    {
        var list = new VirtualList(total: 25, window: 6, step: 4);

        Assert.That(Collect(list, maxScrolls: 8), Has.Count.EqualTo(25));
    }

    [Test]
    public void A_zero_cap_reads_the_current_screen_without_swiping()
    {
        var list = new VirtualList(total: 25, window: 6, step: 4);

        var result = Collect(list, maxScrolls: 0);

        Assert.That(result, Has.Count.EqualTo(6));
        Assert.That(list.Forward + list.Backward, Is.Zero);
    }

    [Test]
    public void A_negative_cap_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScrollingCollector.Collect(() => [], () => "", () => { }, () => { }, -1));
    }
}
