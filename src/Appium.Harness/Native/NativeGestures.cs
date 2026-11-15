using System.Drawing;

using OpenQA.Selenium.Appium;

namespace Appium.Harness;

/// <summary>
/// Swipes, as precise controlled drags rather than flings, so scrolling lands where it is asked
/// to and a tap immediately afterwards is not swallowed by inertia.
///
/// <para>The platforms need different primitives. Android's <c>mobile: scrollGesture</c> drives
/// the scrollable under a band of the screen; XCUITest's <c>mobile: scroll</c> needs an in-tree
/// anchor and is unreliable without one, so iOS gets a timed coordinate drag instead. The
/// geometry is pure and kept separate from the script calls so it can be checked without a
/// device.</para>
/// </summary>
internal sealed class NativeGestures(AppiumDriver driver, bool isAndroid)
{
    /// <summary>
    /// One swipe, aimed by lane: <paramref name="alongFraction"/> is the cross-axis position
    /// (width fraction for a vertical swipe, height fraction for a horizontal one) and
    /// <paramref name="distanceFraction"/> is the travel along the swipe axis.
    /// </summary>
    public void Swipe(SwipeDirection direction, double alongFraction, double distanceFraction)
    {
        var size = driver.Manage().Window.Size;
        var vertical = direction is SwipeDirection.Up or SwipeDirection.Down;

        if (isAndroid)
        {
            if (vertical)
            {
                // scrollGesture drives the scrollable under the band. Note the inversion:
                // a finger moving *up* scrolls the content *down*.
                AndroidGesture("mobile: scrollGesture",
                    left: Band(size.Width, alongFraction, 0.6),
                    top: (int)(size.Height * 0.2),
                    width: (int)(size.Width * 0.6),
                    height: (int)(size.Height * 0.6),
                    direction: direction == SwipeDirection.Up ? "down" : "up",
                    percent: Math.Clamp(distanceFraction * 1.5, 0.1, 1.0));
            }
            else
            {
                AndroidGesture("mobile: swipeGesture",
                    left: Band(size.Width, 0.5, 0.8),
                    top: (int)(size.Height * alongFraction) - 20,
                    width: (int)(size.Width * 0.8),
                    height: 40,
                    direction: direction == SwipeDirection.Left ? "left" : "right",
                    percent: Math.Clamp(distanceFraction, 0.1, 1.0));
            }

            return;
        }

        var towardZero = direction is SwipeDirection.Up or SwipeDirection.Left;

        if (vertical)
        {
            var x = (int)(size.Width * alongFraction);
            var (fromY, toY) = SpanFromCentre(size.Height, distanceFraction, towardZero);
            DragIos(x, fromY, x, toY);
        }
        else
        {
            var y = (int)(size.Height * alongFraction);
            var (fromX, toX) = SpanFromCentre(size.Width, distanceFraction, towardZero);
            DragIos(fromX, y, toX, y);
        }
    }

    /// <summary>One swipe anchored on an element — for gesturing a specific row or card.</summary>
    public void SwipeFrom(AppiumElement element, SwipeDirection direction, double distanceFraction)
    {
        var location = element.Location;
        var elementSize = element.Size;

        if (isAndroid)
        {
            AndroidGesture("mobile: swipeGesture",
                left: location.X,
                top: location.Y,
                width: Math.Max(1, elementSize.Width),
                height: Math.Max(1, elementSize.Height),
                direction: direction.ToString().ToLowerInvariant(),
                percent: Math.Clamp(distanceFraction, 0.1, 1.0));
            return;
        }

        var (from, to) = DragFromElement(new Rectangle(location, elementSize), driver.Manage().Window.Size, direction, distanceFraction);
        DragIos(from.X, from.Y, to.X, to.Y);
    }

    /// <summary>Left/top edge of a band of <paramref name="fraction"/> of the axis, centred at <paramref name="at"/>.</summary>
    internal static int Band(int axisLength, double at, double fraction)
    {
        var band = axisLength * fraction;
        return (int)Math.Clamp(axisLength * at - band / 2.0, 0, axisLength - band);
    }

    /// <summary>A from→to span centred on the axis; <paramref name="towardZero"/> means up/left.</summary>
    internal static (int From, int To) SpanFromCentre(int axisLength, double distanceFraction, bool towardZero)
    {
        var half = axisLength * distanceFraction / 2.0;
        var mid = axisLength / 2.0;
        var (from, to) = towardZero ? (mid + half, mid - half) : (mid - half, mid + half);
        return (ClampToSafeZone(from, axisLength), ClampToSafeZone(to, axisLength));
    }

    /// <summary>A drag from the centre of <paramref name="element"/>, travelling a fraction of the screen.</summary>
    internal static (Point From, Point To) DragFromElement(Rectangle element, Size screen, SwipeDirection direction, double distanceFraction)
    {
        var cx = element.X + element.Width / 2;
        var cy = element.Y + element.Height / 2;
        var to = direction switch
        {
            SwipeDirection.Up => new Point(cx, ClampToSafeZone(cy - screen.Height * distanceFraction, screen.Height)),
            SwipeDirection.Down => new Point(cx, ClampToSafeZone(cy + screen.Height * distanceFraction, screen.Height)),
            SwipeDirection.Left => new Point(ClampToSafeZone(cx - screen.Width * distanceFraction, screen.Width), cy),
            _ => new Point(ClampToSafeZone(cx + screen.Width * distanceFraction, screen.Width), cy),
        };

        return (new Point(cx, cy), to);
    }

    /// <summary>
    /// Keeps a coordinate clear of the system gesture zones at the screen edges — status bar,
    /// home indicator, Android back gesture. A drag that starts in one of those is handled by
    /// the OS, not the app, and simply does nothing.
    /// </summary>
    internal static int ClampToSafeZone(double value, int axisLength) =>
        (int)Math.Clamp(value, axisLength * 0.08, axisLength * 0.92);

    private void AndroidGesture(string script, int left, int top, int width, int height, string direction, double percent) =>
        driver.ExecuteScript(script, new Dictionary<string, object>
        {
            ["left"] = left,
            ["top"] = top,
            ["width"] = width,
            ["height"] = height,
            ["direction"] = direction,
            ["percent"] = percent,
        });

    private void DragIos(int fromX, int fromY, int toX, int toY, double durationSeconds = 0.4) =>
        driver.ExecuteScript("mobile: dragFromToForDuration", new Dictionary<string, object>
        {
            ["duration"] = durationSeconds,
            ["fromX"] = fromX,
            ["fromY"] = fromY,
            ["toX"] = toX,
            ["toY"] = toY,
        });
}
