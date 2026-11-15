namespace Appium.Harness.Tests;

[TestFixture]
public class ProbePayloadTests
{
    private static KeyValuePair<string, string?> P(string name, string? value) => new(name, value);

    [Test]
    public void Formats_into_a_marked_payload()
    {
        var text = ProbePayload.Format([P("TextColor", "#FF7EB4F0"), P("Value", "42")]);

        Assert.That(text, Is.EqualTo("[[probe:TextColor=#FF7EB4F0;Value=42]]"));
    }

    [Test]
    public void An_empty_set_formats_to_an_empty_payload_that_parses_to_nothing()
    {
        var text = ProbePayload.Format([]);

        Assert.That(text, Is.EqualTo("[[probe:]]"));
        Assert.That(ProbePayload.Parse(text), Is.Empty);
    }

    [Test]
    public void Round_trips_awkward_values()
    {
        var values = new Dictionary<string, string?>
        {
            ["plain"] = "abc",
            ["semi"] = "a;b",
            ["equals"] = "a=b",
            ["slash"] = @"a\b",
            ["bracket"] = "a]]b]",
            ["marker"] = "[[probe:x=1]]",
            ["empty"] = "",
            ["null"] = null,
            ["unicode"] = "zażółć 🎨",
            ["spaces"] = "  padded  ",
            ["new line"] = "a\nb",
        };

        var parsed = ProbePayload.Parse(ProbePayload.Format(values));

        Assert.That(parsed, Has.Count.EqualTo(values.Count));
        foreach (var (name, value) in values)
        {
            Assert.That(parsed[name], Is.EqualTo(value ?? ""), name);
        }
    }

    [Test]
    public void Round_trips_a_name_that_needs_escaping()
    {
        var parsed = ProbePayload.Parse(ProbePayload.Format([P("a;b=c", "v")]));

        Assert.That(parsed["a;b=c"], Is.EqualTo("v"));
    }

    [Test]
    public void Finds_the_payload_inside_a_real_accessibility_label()
    {
        var label = "Total XP 120 [[probe:TextColor=#FF7EB4F0]] more text";

        Assert.That(ProbePayload.TryGet(label, "TextColor", out var value), Is.True);
        Assert.That(value, Is.EqualTo("#FF7EB4F0"));
    }

    [Test]
    public void Names_are_case_insensitive()
    {
        Assert.That(ProbePayload.TryGet("[[probe:TextColor=red]]", "textcolor", out var value), Is.True);
        Assert.That(value, Is.EqualTo("red"));
    }

    [Test]
    public void The_last_duplicate_wins()
    {
        Assert.That(ProbePayload.Parse("[[probe:a=1;a=2]]")["a"], Is.EqualTo("2"));
    }

    [Test]
    public void Only_the_first_payload_is_read()
    {
        var parsed = ProbePayload.Parse("[[probe:a=1]] [[probe:b=2]]");

        Assert.That(parsed.Keys, Is.EquivalentTo(new[] { "a" }));
    }

    [Test]
    public void A_missing_property_is_reported_missing()
    {
        Assert.That(ProbePayload.TryGet("[[probe:a=1]]", "b", out var value), Is.False);
        Assert.That(value, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("just a label")]
    [TestCase("[[probe:a=1")]
    [TestCase("[[probe:a=1]")]
    [TestCase("[[probe:a=1\\]]")]
    [TestCase("[[probe:")]
    public void Absent_or_truncated_payloads_parse_to_nothing_without_throwing(string? carrier)
    {
        Assert.That(ProbePayload.Parse(carrier), Is.Empty);
        Assert.That(ProbePayload.TryGet(carrier, "a", out _), Is.False);
    }

    [Test]
    public void Entries_without_a_name_or_an_equals_are_skipped()
    {
        var parsed = ProbePayload.Parse("[[probe:=novalue;orphan;ok=1;=;]]");

        Assert.That(parsed.Keys, Is.EquivalentTo(new[] { "ok" }));
    }

    [Test]
    public void A_value_may_contain_an_unescaped_equals_after_the_first()
    {
        Assert.That(ProbePayload.Parse("[[probe:a=b=c]]")["a"], Is.EqualTo("b=c"));
    }

    [Test]
    public void A_lone_close_bracket_inside_a_value_does_not_end_the_payload()
    {
        Assert.That(ProbePayload.Parse("[[probe:a=x]y;b=2]]")["a"], Is.EqualTo("x]y"));
    }

    [TestCase("[[probe:a=1]]", "")]
    [TestCase("120 [[probe:a=1]]", "120")]
    [TestCase("[[probe:a=1]] 120", "120")]
    [TestCase("Total [[probe:a=1;b=x\\]y]] XP", "Total  XP")]
    [TestCase("plain label", "plain label")]
    [TestCase("", "")]
    [TestCase("[[probe:a=1", "[[probe:a=1")]
    [TestCase("[[probe:a=1]", "[[probe:a=1]")]
    public void Strip_removes_the_payload_and_leaves_everything_else(string carrier, string expected)
    {
        Assert.That(ProbePayload.Strip(carrier), Is.EqualTo(expected));
    }

    [Test]
    public void Strip_of_null_is_empty()
    {
        Assert.That(ProbePayload.Strip(null), Is.Empty);
    }

    [Test]
    public void Strip_undoes_Format()
    {
        var payload = ProbePayload.Format([P("a", "1]]2"), P("b", "x")]);

        Assert.That(ProbePayload.Strip("42 " + payload), Is.EqualTo("42"));
    }

    [Test]
    public void Format_requires_its_argument()
    {
        Assert.Throws<ArgumentNullException>(() => ProbePayload.Format(null!));
    }

    [Test]
    public void The_shared_property_names_are_the_MAUI_names()
    {
        Assert.That(ProbeProperties.TextColor, Is.EqualTo("TextColor"));
        Assert.That(ProbeProperties.BackgroundColor, Is.EqualTo("BackgroundColor"));
        Assert.That(ProbeProperties.Value, Is.EqualTo("Value"));
    }
}
