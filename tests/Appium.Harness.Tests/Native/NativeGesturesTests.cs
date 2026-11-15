using System.Drawing;

namespace Appium.Harness.Tests;

/// <summary>The swipe geometry, which decides whether a gesture lands in the app or in the OS's edge zones.</summary>
[TestFixture]
public class NativeGesturesTests
{
    [TestCase(1000, 0.5, 0.6, 200)] // centred
    [TestCase(1000, 0.0, 0.6, 0)]   // clamped to the left edge, not negative
    [TestCase(1000, 1.0, 0.6, 400)] // clamped so the band ends at the right edge
    public void Band_is_centred_and_stays_on_screen(int axis, double at, double fraction, int expectedStart)
    {
        Assert.That(NativeGestures.Band(axis, at, fraction), Is.EqualTo(expectedStart));
    }

    [Test]
    public void Span_toward_zero_runs_from_below_the_centre_to_above_it()
    {
        var (from, to) = NativeGestures.SpanFromCentre(1000, 0.6, towardZero: true);

        Assert.That((from, to), Is.EqualTo((800, 200)));
    }

    [Test]
    public void Span_away_from_zero_runs_the_other_way()
    {
        var (from, to) = NativeGestures.SpanFromCentre(1000, 0.6, towardZero: false);

        Assert.That((from, to), Is.EqualTo((200, 800)));
    }

    [Test]
    public void A_full_length_span_is_kept_out_of_the_system_gesture_zones()
    {
        var (from, to) = NativeGestures.SpanFromCentre(1000, 1.0, towardZero: true);

        Assert.That((from, to), Is.EqualTo((920, 80)));
    }

    [TestCase(-50, 80)]
    [TestCase(500, 500)]
    [TestCase(2000, 920)]
    public void ClampToSafeZone_keeps_eight_percent_clear_at_each_end(double value, int expected)
    {
        Assert.That(NativeGestures.ClampToSafeZone(value, 1000), Is.EqualTo(expected));
    }

    [TestCase(SwipeDirection.Up, 150, 80)]     // would leave the top; clamped
    [TestCase(SwipeDirection.Down, 150, 920)]  // would leave the bottom; clamped
    [TestCase(SwipeDirection.Left, 80, 450)]
    [TestCase(SwipeDirection.Right, 650, 450)]
    public void DragFromElement_starts_at_the_element_centre(SwipeDirection direction, int toX, int toY)
    {
        var element = new Rectangle(100, 400, 100, 100); // centre (150, 450)

        var (from, to) = NativeGestures.DragFromElement(element, new Size(1000, 1000), direction, 0.5);

        Assert.That(from, Is.EqualTo(new Point(150, 450)));
        Assert.That(to, Is.EqualTo(new Point(toX, toY)));
    }

    [Test]
    public void DragFromElement_on_a_vertical_swipe_keeps_x_and_clamps_y()
    {
        var (_, to) = NativeGestures.DragFromElement(new Rectangle(0, 100, 200, 0), new Size(1000, 1000), SwipeDirection.Up, 0.5);

        Assert.That(to, Is.EqualTo(new Point(100, 80)));
    }
}
