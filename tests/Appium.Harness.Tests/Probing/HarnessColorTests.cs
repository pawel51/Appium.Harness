namespace Appium.Harness.Tests;

[TestFixture]
public class HarnessColorTests
{
    [TestCase("#F00", 255, 255, 0, 0)]
    [TestCase("#0f8", 255, 0, 255, 136)]
    [TestCase("#7EB4F0", 255, 126, 180, 240)]
    [TestCase("#7eb4f0", 255, 126, 180, 240)]
    [TestCase("#80112233", 128, 17, 34, 51)]
    [TestCase("  #FF000000  ", 255, 0, 0, 0)]
    [TestCase("rgb(126, 180, 240)", 255, 126, 180, 240)]
    [TestCase("rgb(126,180,240)", 255, 126, 180, 240)]
    [TestCase("RGB(1, 2, 3)", 255, 1, 2, 3)]
    [TestCase("rgba(10, 20, 30, 0.5)", 128, 10, 20, 30)]
    [TestCase("rgba(10, 20, 30, 1)", 255, 10, 20, 30)]
    [TestCase("rgba(10, 20, 30, 0)", 0, 10, 20, 30)]
    [TestCase("rgba(0, 0, 0, 0)", 0, 0, 0, 0)]
    [TestCase("rgb(10 20 30)", 255, 10, 20, 30)]
    [TestCase("rgb(10 20 30 / 50%)", 128, 10, 20, 30)]
    [TestCase("rgb(100%, 0%, 50%)", 255, 255, 0, 128)]
    [TestCase("rgb(12.4, 12.6, 0.2)", 255, 12, 13, 0)]
    [TestCase("rgb(300, -5, 0)", 255, 255, 0, 0)]
    [TestCase("transparent", 0, 0, 0, 0)]
    [TestCase("Transparent", 0, 0, 0, 0)]
    public void Parses_every_supported_notation(string text, int a, int r, int g, int b)
    {
        var color = HarnessColor.Parse(text);

        Assert.That((color.A, color.R, color.G, color.B), Is.EqualTo(((byte)a, (byte)r, (byte)g, (byte)b)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("red")]
    [TestCase("#")]
    [TestCase("#12")]
    [TestCase("#1234")]
    [TestCase("#12345")]
    [TestCase("#1234567")]
    [TestCase("#12345678A")]
    [TestCase("#GGGGGG")]
    [TestCase("rgb(1, 2)")]
    [TestCase("rgb(1, 2, 3, 4, 5)")]
    [TestCase("rgb(a, b, c)")]
    [TestCase("rgb(1, 2, 3, x)")]
    [TestCase("rgb(1, 2, NaN)")]
    [TestCase("rgb(1, 2, 3")]
    [TestCase("hsl(10, 20%, 30%)")]
    [TestCase("rgb 1 2 3")]
    public void Rejects_what_is_not_a_colour(string? text)
    {
        Assert.That(HarnessColor.TryParse(text, out var color), Is.False);
        Assert.That(color, Is.EqualTo(default(HarnessColor)));
    }

    [Test]
    public void Parse_throws_FormatException_naming_the_input_and_the_accepted_forms()
    {
        var ex = Assert.Throws<FormatException>(() => HarnessColor.Parse("chartreuse"));

        Assert.That(ex!.Message, Does.Contain("chartreuse").And.Contain("#AARRGGBB").And.Contain("rgba("));
    }

    [Test]
    public void Eight_digit_hex_is_AARRGGBB_not_CSS_RRGGBBAA()
    {
        var color = HarnessColor.Parse("#33FF0000");

        Assert.That((color.A, color.R), Is.EqualTo(((byte)0x33, (byte)0xFF)));
    }

    [Test]
    public void The_same_colour_in_hex_and_css_is_equal()
    {
        Assert.That(HarnessColor.Parse("#7EB4F0"), Is.EqualTo(HarnessColor.Parse("rgb(126, 180, 240)")));
        Assert.That(HarnessColor.Parse("#FF7EB4F0"), Is.EqualTo(HarnessColor.Parse("rgba(126, 180, 240, 1)")));
    }

    [Test]
    public void ToHex_is_AARRGGBB_and_round_trips()
    {
        var color = new HarnessColor(0x12, 0xAB, 0x00, 0xFF);

        Assert.That(color.ToHex(), Is.EqualTo("#12AB00FF"));
        Assert.That(color.ToString(), Is.EqualTo("#12AB00FF"));
        Assert.That(HarnessColor.Parse(color.ToHex()), Is.EqualTo(color));
    }

    [Test]
    public void A_string_converts_implicitly()
    {
        HarnessColor color = "#102030";

        Assert.That(color, Is.EqualTo(new HarnessColor(255, 0x10, 0x20, 0x30)));
        Assert.Throws<FormatException>(() => { HarnessColor bad = "nope"; _ = bad; });
    }

    [TestCase(0, 0, true)]
    [TestCase(1, 0, false)]
    [TestCase(1, 1, true)]
    [TestCase(5, 4, false)]
    [TestCase(5, 5, true)]
    [TestCase(-5, 5, true)]
    [TestCase(-6, 5, false)]
    public void IsCloseTo_compares_every_channel_within_tolerance(int delta, int tolerance, bool expected)
    {
        var actual = new HarnessColor(255, (byte)(100 + delta), 100, 100);

        Assert.That(actual.IsCloseTo(new HarnessColor(255, 100, 100, 100), tolerance), Is.EqualTo(expected));
    }

    [Test]
    public void IsCloseTo_includes_alpha_and_every_channel()
    {
        var baseline = new HarnessColor(255, 10, 20, 30);

        Assert.That(new HarnessColor(250, 10, 20, 30).IsCloseTo(baseline, 4), Is.False, "alpha");
        Assert.That(new HarnessColor(255, 10, 25, 30).IsCloseTo(baseline, 4), Is.False, "green");
        Assert.That(new HarnessColor(255, 10, 20, 35).IsCloseTo(baseline, 4), Is.False, "blue");
        Assert.That(new HarnessColor(253, 12, 22, 28).IsCloseTo(baseline, 2), Is.True);
    }

    [Test]
    public void IsCloseTo_rejects_a_negative_tolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => default(HarnessColor).IsCloseTo(default, -1));
    }
}
