using System.Globalization;
using Appium.Harness.Maui;

namespace Appium.Harness.Maui.Tests;

// TestProbe.IsEnabled is process-wide, so these must not run alongside each other.
[NonParallelizable]
public class TestProbeTests
{
    [SetUp]
    public void Enable() => TestProbe.IsEnabled = true;

    [TearDown]
    public void Disable() => TestProbe.IsEnabled = false;

    [Test]
    public void Publishes_named_properties_and_the_bound_value()
    {
        var label = new Label { TextColor = Color.FromArgb("#7EB4F0") };
        TestProbe.SetProperties(label, "TextColor");
        TestProbe.SetValue(label, 120);

        var published = ProbePayload.Parse(TestProbe.PayloadFor(label));

        Assert.That(published, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["TextColor"] = "#FF7EB4F0",
            ["Value"] = "120",
        }));
    }

    [Test]
    public void Publishes_nothing_when_nothing_is_asked_for()
    {
        Assert.That(TestProbe.PayloadFor(new Label()), Is.Null);
    }

    [Test]
    public void Skips_a_property_the_view_does_not_have()
    {
        var label = new Label { TextColor = Colors.Red };
        TestProbe.SetProperties(label, "NoSuchProperty, TextColor");

        Assert.That(ProbePayload.Parse(TestProbe.PayloadFor(label)).Keys, Is.EquivalentTo(new[] { "TextColor" }));
    }

    [Test]
    public void The_attached_value_owns_the_name_Value()
    {
        // Naming "Value" in Properties must not publish it twice, or from the wrong source.
        var slider = new Slider { Maximum = 10, Value = 3 };
        TestProbe.SetProperties(slider, "Value");
        TestProbe.SetValue(slider, "from-the-view-model");

        Assert.That(TestProbe.PayloadFor(slider), Is.EqualTo("[[probe:Value=from-the-view-model]]"));
    }

    [Test]
    public void A_view_is_not_tracked_until_probes_are_enabled()
    {
        TestProbe.IsEnabled = false;
        var label = new Label();
        TestProbe.SetProperties(label, "TextColor");

        Assert.That(TestProbe.IsTracked(label), Is.False);

        TestProbe.IsEnabled = true;
        TestProbe.SetProperties(label, "TextColor,BackgroundColor");

        Assert.That(TestProbe.IsTracked(label), Is.True);
    }

    [Test]
    public void UseTestProbes_enables_them()
    {
        TestProbe.IsEnabled = false;

        MauiApp.CreateBuilder().UseTestProbes();

        Assert.That(TestProbe.IsEnabled, Is.True);
    }

    [Test]
    public void Formats_numbers_independently_of_the_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.That(TestProbe.Format(1.5), Is.EqualTo("1.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void Formats_colours_brushes_and_flags()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestProbe.Format(Color.FromRgba(126, 180, 240, 128)), Is.EqualTo("#807EB4F0"));
            Assert.That(TestProbe.Format(new SolidColorBrush(Colors.Blue)), Is.EqualTo("#FF0000FF"));
            Assert.That(TestProbe.Format(new LinearGradientBrush()), Is.Null, "a gradient has no single colour");
            Assert.That(TestProbe.Format(true), Is.EqualTo("true"));
            Assert.That(TestProbe.Format(null), Is.Null);
        });
    }
}
