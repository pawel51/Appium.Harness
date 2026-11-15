using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class NativeReaderTests
{
    private static IWebElement Element(params (string Attribute, string? Value)[] attributes)
    {
        var element = Substitute.For<IWebElement>();
        element.GetAttribute(Arg.Any<string>()).Returns(call =>
            attributes.FirstOrDefault(a => a.Attribute == call.Arg<string>()).Value);
        return element;
    }

    private static INativeScreen Screen(bool android, IWebElement? element = null)
    {
        var screen = Substitute.For<INativeScreen>();
        screen.IsAndroid.Returns(android);
        if (element is not null)
        {
            screen.Find(Arg.Any<string>()).Returns(element);
        }

        return screen;
    }

    /// <summary>A virtualised list on a fake screen: a window of rows is in the tree and swiping moves it.</summary>
    private sealed class ListScreen(bool android, int total, int window, int step, int start = 0)
    {
        private int _offset = start;

        public INativeScreen Build()
        {
            var screen = Substitute.For<INativeScreen>();
            screen.IsAndroid.Returns(android);
            screen.PageSource().Returns(_ => $"offset={_offset}");
            screen.When(s => s.SwipeUp()).Do(_ => _offset = Math.Min(_offset + step, Math.Max(total - window, 0)));
            screen.When(s => s.SwipeDown()).Do(_ => _offset = Math.Max(_offset - step, 0));
            screen.FindByIdRegex(Arg.Any<string>()).Returns(call =>
            {
                var regex = new System.Text.RegularExpressions.Regex($"^{call.Arg<string>()}$");
                return (IReadOnlyList<IWebElement>)Enumerable.Range(_offset, Math.Min(window, total - _offset))
                    .Select(i => $"row-{i}")
                    .Where(id => regex.IsMatch(id))
                    .Select(id => android ? Element(("resource-id", $"com.example:id/{id}")) : Element(("name", id)))
                    .ToList();
            });
            return screen;
        }
    }

    #region CountIds

    [Test]
    public void CountIds_without_scrolling_counts_what_is_in_the_tree()
    {
        var screen = Screen(android: true);
        var rows = new List<IWebElement> { Element(), Element(), Element() };
        screen.FindByIdRegex("row-.*").Returns(rows);

        Assert.That(new NativeReader(screen).CountIds("row-.*", scroll: false, maxScrolls: 15), Is.EqualTo(3));
        screen.DidNotReceive().SwipeUp();
        screen.DidNotReceive().SwipeDown();
    }

    [Test]
    public void CountIds_without_scrolling_counts_zero_for_no_match()
    {
        var screen = Screen(android: false);
        screen.FindByIdRegex(Arg.Any<string>()).Returns([]);

        Assert.That(new NativeReader(screen).CountIds("row-.*", false, 15), Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CountIds_with_scrolling_counts_the_whole_virtualised_list_on_both_platforms(bool android)
    {
        var screen = new ListScreen(android, total: 25, window: 6, step: 4).Build();

        Assert.That(new NativeReader(screen).CountIds("row-.*", scroll: true, maxScrolls: 15), Is.EqualTo(25));
    }

    [Test]
    public void CountIds_without_scrolling_undercounts_the_same_list_which_is_why_scroll_exists()
    {
        var screen = new ListScreen(android: true, total: 25, window: 6, step: 4).Build();

        Assert.That(new NativeReader(screen).CountIds("row-.*", scroll: false, maxScrolls: 15), Is.EqualTo(6));
    }

    [Test]
    public void CountIds_with_scrolling_only_counts_ids_that_match()
    {
        var screen = new ListScreen(android: true, total: 25, window: 6, step: 4).Build();

        Assert.That(new NativeReader(screen).CountIds("row-1.*", scroll: true, maxScrolls: 15), Is.EqualTo(11)); // 1, 10..19
    }

    [Test]
    public void CountIds_with_scrolling_skips_rows_that_are_recycled_mid_read()
    {
        var stale = Substitute.For<IWebElement>();
        stale.GetAttribute(Arg.Any<string>()).Throws(new StaleElementReferenceException("recycled"));
        var screen = Screen(android: true);
        screen.PageSource().Returns("static");
        var rows = new List<IWebElement> { stale, Element(("resource-id", "com.example:id/row-1")) };
        screen.FindByIdRegex(Arg.Any<string>()).Returns(rows);

        Assert.That(new NativeReader(screen).CountIds("row-.*", scroll: true, maxScrolls: 15), Is.EqualTo(1));
    }

    [Test]
    public void CountIds_with_scrolling_ignores_blank_ids()
    {
        var screen = Screen(android: false);
        screen.PageSource().Returns("static");
        var rows = new List<IWebElement> { Element(("name", " ")), Element(("name", null)), Element(("name", "row-1")) };
        screen.FindByIdRegex(Arg.Any<string>()).Returns(rows);

        Assert.That(new NativeReader(screen).CountIds("row-.*", scroll: true, maxScrolls: 15), Is.EqualTo(1));
    }

    [Test]
    public void The_two_counting_modes_differ_when_ids_repeat_which_is_documented()
    {
        var screen = Screen(android: false);
        screen.PageSource().Returns("static");
        // A container and its child both carry the id 'row-1'.
        var rows = new List<IWebElement> { Element(("name", "row-1")), Element(("name", "row-1")), Element(("name", "row-2")) };
        screen.FindByIdRegex(Arg.Any<string>()).Returns(rows);
        var reader = new NativeReader(screen);

        Assert.That(reader.CountIds("row-.*", scroll: false, maxScrolls: 15), Is.EqualTo(3), "elements");
        Assert.That(reader.CountIds("row-.*", scroll: true, maxScrolls: 15), Is.EqualTo(2), "distinct ids");
    }

    [Test]
    public void CountIds_with_scrolling_surfaces_a_list_that_never_settles()
    {
        var screen = Screen(android: true);
        var n = 0;
        screen.PageSource().Returns(_ => $"page-{n++}");
        var rows = new List<IWebElement>();
        screen.FindByIdRegex(Arg.Any<string>()).Returns(rows);

        Assert.Throws<InvalidOperationException>(() => new NativeReader(screen).CountIds("row-.*", scroll: true, maxScrolls: 3));
    }

    #endregion

    #region Probe properties

    [Test]
    public void Android_reads_the_payload_from_content_desc()
    {
        var screen = Screen(android: true, Element(("content-desc", "[[probe:TextColor=#FF7EB4F0;Value=42]]")));
        var reader = new NativeReader(screen);

        Assert.That(reader.ReadProperty("xp", "TextColor"), Is.EqualTo("#FF7EB4F0"));
        Assert.That(reader.ReadProperty("xp", "Value"), Is.EqualTo("42"));
    }

    [Test]
    public void Android_does_not_look_in_the_value_attribute()
    {
        var screen = Screen(android: true, Element(("value", "[[probe:Value=1]]")));

        Assert.That(new NativeReader(screen).ReadProperty("xp", "Value"), Is.Null);
    }

    [Test]
    public void Ios_reads_the_payload_from_value()
    {
        var screen = Screen(android: false, Element(("value", "[[probe:Value=42]]")));

        Assert.That(new NativeReader(screen).ReadProperty("xp", "Value"), Is.EqualTo("42"));
    }

    [Test]
    public void Ios_falls_back_to_label_when_value_has_no_payload()
    {
        var screen = Screen(android: false, Element(("value", "something else"), ("label", "120 [[probe:Value=7]]")));

        Assert.That(new NativeReader(screen).ReadProperty("xp", "Value"), Is.EqualTo("7"));
    }

    [Test]
    public void A_property_the_app_did_not_publish_reads_null()
    {
        var screen = Screen(android: true, Element(("content-desc", "[[probe:Other=1]]")));

        Assert.That(new NativeReader(screen).ReadProperty("xp", "Value"), Is.Null);
    }

    [Test]
    public void An_element_without_any_payload_reads_null_and_has_no_properties()
    {
        var reader = new NativeReader(Screen(android: true, Element(("content-desc", "Just a label"))));

        Assert.That(reader.ReadProperty("xp", "Value"), Is.Null);
        Assert.That(reader.ReadProperties("xp"), Is.Empty);
    }

    [Test]
    public void An_absent_element_propagates_the_not_found_error_so_the_caller_can_poll()
    {
        var screen = Screen(android: true);
        screen.Find("ghost").Throws(new NoSuchElementException("no such element"));
        var reader = new NativeReader(screen);

        Assert.Throws<NoSuchElementException>(() => reader.ReadProperty("ghost", "Value"));
        Assert.Throws<NoSuchElementException>(() => reader.ReadProperties("ghost"));
    }

    [Test]
    public void ReadProperties_returns_everything_published_and_prefers_the_first_carrier_that_has_a_payload()
    {
        var screen = Screen(android: false, Element(("value", "[[probe:A=1;B=2]]"), ("label", "[[probe:C=3]]")));

        var properties = new NativeReader(screen).ReadProperties("xp");

        Assert.That(properties.Keys, Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public void ReadColor_parses_the_published_colour()
    {
        var reader = new NativeReader(Screen(android: true, Element(("content-desc", "[[probe:TextColor=#FF7EB4F0]]"))));

        Assert.That(reader.ReadColor("xp", "TextColor"), Is.EqualTo(new HarnessColor(255, 126, 180, 240)));
        Assert.That(reader.ReadColor("xp", "BackgroundColor"), Is.Null);
    }

    [Test]
    public void ReadColor_throws_when_what_was_published_is_not_a_colour()
    {
        var reader = new NativeReader(Screen(android: true, Element(("content-desc", "[[probe:TextColor=blueish]]"))));

        Assert.Throws<FormatException>(() => reader.ReadColor("xp", "TextColor"));
    }

    [Test]
    public void The_carriers_differ_by_platform()
    {
        Assert.That(new NativeReader(Screen(android: true)).Carriers, Is.EqualTo(new[] { "content-desc" }));
        Assert.That(new NativeReader(Screen(android: false)).Carriers, Is.EqualTo(new[] { "value", "label" }));
    }

    #endregion

    [TestCase("com.example.app:id/row-1", "row-1")]
    [TestCase("pl.vtome.dev.mylocalapp:id/profile-xp", "profile-xp")]
    [TestCase("row-1", "row-1")]
    [TestCase("a/b", "a/b")]
    public void SimpleId_strips_the_android_package_qualifier(string raw, string expected)
    {
        Assert.That(NativeLocators.SimpleId(raw), Is.EqualTo(expected));
    }
}
