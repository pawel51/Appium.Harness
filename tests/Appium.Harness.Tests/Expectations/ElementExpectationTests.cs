using OpenQA.Selenium;

namespace Appium.Harness.Tests;

[TestFixture]
public class ElementExpectationTests
{
    private static ElementExpectation Expect(FakeProbe probe, ExpectSettings? settings = null) =>
        new(probe, settings ?? Settings.Fast());

    private static string FailureOf(TestDelegate act) =>
        Assert.Throws<HarnessAssertionException>(act)!.Message;

    #region Text

    [Test]
    public void HasText_passes_when_the_text_matches()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Text = () => "120" }).HasText("120"));
    }

    [Test]
    public void HasText_waits_for_the_text_to_arrive()
    {
        var probe = new FakeProbe { Text = FakeProbe.Eventually(badReads: 4, bad: "0", good: "120") };

        Expect(probe, Settings.Fast(timeoutMs: 2000)).HasText("120");

        Assert.That(probe.TextReads, Is.EqualTo(5));
    }

    [Test]
    public void HasText_failure_names_the_element_the_expected_and_the_last_actual_text()
    {
        var message = FailureOf(() => Expect(new FakeProbe { Subject = "'profile-xp'", Text = () => "0" }).HasText("120"));

        Assert.That(message, Does.Contain("Expected 'profile-xp' to have text '120'").And.Contain("it was '0'"));
    }

    [Test]
    public void HasText_is_exact_by_default()
    {
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Text = () => "Hello" }).HasText("hello"));
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Text = () => " Hello " }).HasText("Hello"));
    }

    [Test]
    public void HasText_can_ignore_case()
    {
        Assert.DoesNotThrow(() =>
            Expect(new FakeProbe { Text = () => "HELLO" }).HasText("hello", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void HasText_can_trim_the_actual_text()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Text = () => "  Hello\n" }).HasText("Hello", trim: true));
    }

    [Test]
    public void HasText_distinguishes_empty_text()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Text = () => "" }).HasText(""));
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Text = () => "x" }).HasText(""));
    }

    [Test]
    public void ContainsText_passes_on_a_fragment_and_fails_otherwise()
    {
        var probe = new FakeProbe { Text = () => "Level 7 — Explorer" };

        Assert.DoesNotThrow(() => Expect(probe).ContainsText("Explorer"));
        Assert.DoesNotThrow(() => Expect(probe).ContainsText("explorer", StringComparison.OrdinalIgnoreCase));
        Assert.That(FailureOf(() => Expect(probe).ContainsText("Master")), Does.Contain("contain text 'Master'"));
    }

    [Test]
    public void MatchesText_uses_a_regex_found_anywhere_in_the_text()
    {
        var probe = new FakeProbe { Text = () => "120 XP" };

        Assert.DoesNotThrow(() => Expect(probe).MatchesText(@"\d+ XP"));
        Assert.DoesNotThrow(() => Expect(probe).MatchesText(@"^\d+ XP$"));
        Assert.DoesNotThrow(() => Expect(probe).MatchesText("xp", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Assert.That(FailureOf(() => Expect(probe).MatchesText(@"^\d+$")), Does.Contain(@"matching /^\d+$/"));
    }

    [Test]
    public void MatchesText_rejects_an_invalid_pattern_immediately()
    {
        Assert.Catch<ArgumentException>(() => Expect(new FakeProbe()).MatchesText("(unclosed"));
    }

    [Test]
    public void Text_arguments_are_validated()
    {
        Assert.Throws<ArgumentNullException>(() => Expect(new FakeProbe()).HasText(null!));
        Assert.Throws<ArgumentNullException>(() => Expect(new FakeProbe()).ContainsText(null!));
    }

    #endregion

    #region State

    [Test]
    public void IsVisible_passes_for_a_visible_element()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Visible = () => true }).IsVisible());
    }

    [Test]
    public void IsVisible_waits_for_the_element_to_appear()
    {
        var probe = new FakeProbe { Visible = FakeProbe.Eventually(badReads: 3, bad: false, good: true) };

        Assert.DoesNotThrow(() => Expect(probe, Settings.Fast(timeoutMs: 2000)).IsVisible());
    }

    [Test]
    public void IsVisible_failure_says_not_visible_and_lists_what_was_on_screen()
    {
        var message = FailureOf(() => Expect(new FakeProbe { Subject = "'save'", Visible = () => false }).IsVisible());

        Assert.That(message, Does.Contain("Expected 'save' to be visible").And.Contain("not visible"));
        Assert.That(message, Does.Contain("Visible ids: a, b"));
    }

    [Test]
    public void Not_IsVisible_waits_for_the_element_to_go_away()
    {
        var probe = new FakeProbe { Visible = FakeProbe.Eventually(badReads: 3, bad: true, good: false) };

        Assert.DoesNotThrow(() => Expect(probe, Settings.Fast(timeoutMs: 2000)).Not.IsVisible());
    }

    [Test]
    public void Not_IsVisible_failure_reads_as_a_negation()
    {
        var message = FailureOf(() => Expect(new FakeProbe { Subject = "'banner'" }).Not.IsVisible());

        Assert.That(message, Does.Contain("Expected 'banner' not to be visible, but it was visible"));
    }

    [Test]
    public void IsEnabled_and_its_negation()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Enabled = () => true }).IsEnabled());
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Enabled = () => false }).Not.IsEnabled());
        Assert.That(FailureOf(() => Expect(new FakeProbe { Enabled = () => false }).IsEnabled()),
            Does.Contain("to be enabled").And.Contain("it was disabled"));
    }

    [Test]
    public void IsToggledOn_and_its_negation()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Toggled = () => true }).IsToggledOn());
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Toggled = () => false }).Not.IsToggledOn());
        Assert.That(FailureOf(() => Expect(new FakeProbe { Toggled = () => false }).IsToggledOn()),
            Does.Contain("to be toggled on").And.Contain("it was off"));
    }

    #endregion

    #region Colour

    [Test]
    public void HasTextColor_compares_what_the_app_published_with_what_the_test_expects()
    {
        var probe = new FakeProbe { Color = name => name == "TextColor" ? "#FF7EB4F0" : null };

        Assert.DoesNotThrow(() => Expect(probe).HasTextColor("#7EB4F0"));
        Assert.DoesNotThrow(() => Expect(probe).HasTextColor("rgb(126, 180, 240)"));
        Assert.DoesNotThrow(() => Expect(probe).HasTextColor(new HarnessColor(255, 126, 180, 240)));
    }

    [Test]
    public void HasBackgroundColor_reads_the_platforms_background_property()
    {
        var asked = new List<string>();
        var probe = new FakeProbe
        {
            BackgroundColorName = "background-color",
            Color = name => { asked.Add(name); return "rgb(0, 0, 0)"; },
        };

        Expect(probe).HasBackgroundColor("#000000");

        Assert.That(asked, Has.All.EqualTo("background-color"));
    }

    [Test]
    public void HasColor_reads_any_named_colour_property()
    {
        var probe = new FakeProbe { Color = name => name == "Stroke" ? "#FFFF0000" : "#FF000000" };

        Assert.DoesNotThrow(() => Expect(probe).HasColor("Stroke", "#FF0000"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).HasColor("Fill", "#FF0000"));
    }

    [Test]
    public void Colour_failure_shows_both_colours_in_the_same_notation()
    {
        var probe = new FakeProbe { Subject = "'xp'", Color = _ => "rgb(255, 0, 0)" };

        var message = FailureOf(() => Expect(probe).HasTextColor("#7EB4F0"));

        Assert.That(message, Does.Contain("Expected 'xp' to have TextColor #FF7EB4F0"));
        Assert.That(message, Does.Contain("it was #FFFF0000 (read as 'rgb(255, 0, 0)')"));
    }

    [Test]
    public void Colour_tolerance_absorbs_rounding_and_is_shown_in_the_failure()
    {
        var probe = new FakeProbe { Color = _ => "#FF7EB4F2" };

        Assert.DoesNotThrow(() => Expect(probe).HasTextColor("#7EB4F0", tolerance: 2));
        var message = FailureOf(() => Expect(probe).HasTextColor("#7EB4F0", tolerance: 1));
        Assert.That(message, Does.Contain("(±1)"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).HasTextColor("#7EB4F0"));
    }

    [Test]
    public void A_colour_the_app_did_not_publish_fails_with_a_clear_reason()
    {
        var message = FailureOf(() => Expect(new FakeProbe { Color = _ => null }).HasTextColor("#000"));

        Assert.That(message, Does.Contain("<not published>"));
    }

    [Test]
    public void A_published_value_that_is_not_a_colour_is_called_out()
    {
        var message = FailureOf(() => Expect(new FakeProbe { Color = _ => "blueish" }).HasTextColor("#000"));

        Assert.That(message, Does.Contain("'blueish' (not a colour)"));
    }

    [Test]
    public void Not_HasTextColor_passes_for_a_different_colour()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Color = _ => "#FF000000" }).Not.HasTextColor("#FFFFFF"));
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Color = _ => "#FFFFFFFF" }).Not.HasTextColor("#FFFFFF"));
    }

    [Test]
    public void Colour_arguments_are_validated()
    {
        Assert.Throws<ArgumentException>(() => Expect(new FakeProbe()).HasColor(" ", "#000"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Expect(new FakeProbe()).HasTextColor("#000", tolerance: -1));
    }

    #endregion

    #region Attributes and properties

    [Test]
    public void HasAttribute_reads_the_raw_platform_attribute()
    {
        var probe = new FakeProbe { Attribute = name => name == "content-desc" ? "Edit profile" : null };

        Assert.DoesNotThrow(() => Expect(probe).HasAttribute("content-desc", "Edit profile"));
        Assert.DoesNotThrow(() => Expect(probe).HasAttribute("missing", null));
        Assert.DoesNotThrow(() => Expect(probe).HasAttribute("content-desc", "edit PROFILE", StringComparison.OrdinalIgnoreCase));
        Assert.That(FailureOf(() => Expect(probe).HasAttribute("content-desc", "Other")),
            Does.Contain("have attribute 'content-desc' = 'Other'").And.Contain("it was 'Edit profile'"));
    }

    [Test]
    public void HasProperty_compares_a_published_value()
    {
        var probe = new FakeProbe { Property = name => name == "Value" ? "42" : null };

        Assert.DoesNotThrow(() => Expect(probe).HasProperty("Value", "42"));
        Assert.That(FailureOf(() => Expect(probe).HasProperty("Value", "43")),
            Does.Contain("have property 'Value' = '43'").And.Contain("it was '42'"));
    }

    [Test]
    public void HasProperty_reports_an_unpublished_property()
    {
        var message = FailureOf(() => Expect(new FakeProbe()).HasProperty("Nope", "x"));

        Assert.That(message, Does.Contain("<not published>"));
    }

    [Test]
    public void HasProperty_with_a_predicate_describes_itself_in_the_failure()
    {
        var probe = new FakeProbe { Property = _ => "7" };

        Assert.DoesNotThrow(() => Expect(probe).HasProperty("Value", raw => int.Parse(raw!) > 5, "be greater than 5"));
        Assert.That(FailureOf(() => Expect(probe).HasProperty("Value", raw => int.Parse(raw!) > 50, "be greater than 50")),
            Does.Contain("satisfies: be greater than 50").And.Contain("it was '7'"));
    }

    [Test]
    public void HasProperty_predicate_receives_null_for_an_unpublished_property()
    {
        string? received = "sentinel";

        Expect(new FakeProbe()).HasProperty("Value", raw => { received = raw; return true; }, "anything");

        Assert.That(received, Is.Null);
    }

    [Test]
    public void HasProperty_int_parses_the_published_value()
    {
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Property = _ => "42" }).HasProperty("Value", 42));
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Property = _ => "42" }).HasProperty("Value", 43));
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Property = _ => "forty-two" }).HasProperty("Value", 42));
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Property = _ => null }).HasProperty("Value", 42));
    }

    [Test]
    public void Property_arguments_are_validated()
    {
        var e = Expect(new FakeProbe());
        Assert.Throws<ArgumentException>(() => e.HasProperty("", "x"));
        Assert.Throws<ArgumentNullException>(() => e.HasProperty("Value", (string)null!));
        Assert.Throws<ArgumentNullException>(() => e.HasProperty("Value", (Func<string?, bool>)null!, "d"));
        Assert.Throws<ArgumentException>(() => e.HasProperty("Value", _ => true, " "));
        Assert.Throws<ArgumentException>(() => e.HasAttribute("", "x"));
    }

    #endregion

    #region An element that is not there

    [Test]
    public void An_absent_element_fails_a_check_and_says_it_could_not_be_read()
    {
        var probe = new FakeProbe { Subject = "'ghost'", Text = FakeProbe.Absent<string>() };

        var message = FailureOf(() => Expect(probe).HasText("x"));

        Assert.That(message, Does.Contain("Expected 'ghost' to have text 'x'").And.Contain("could not be read").And.Contain("not in the tree"));
    }

    [Test]
    public void An_absent_element_satisfies_neither_polarity_of_a_value_check()
    {
        var probe = new FakeProbe { Text = FakeProbe.Absent<string>(), Color = _ => throw new NoSuchElementException("no such element: not in the tree") };

        Assert.Throws<HarnessAssertionException>(() => Expect(probe).HasText("x"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.HasText("x"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).HasTextColor("#000"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.HasTextColor("#000"));
    }

    [Test]
    public void An_element_that_appears_late_is_found()
    {
        var reads = 0;
        var probe = new FakeProbe { Text = () => ++reads < 4 ? throw new NoSuchElementException("not yet") : "ready" };

        Assert.DoesNotThrow(() => Expect(probe, Settings.Fast(timeoutMs: 2000)).HasText("ready"));
    }

    #endregion

    #region Unpublished values satisfy neither polarity

    [Test]
    public void Not_HasTextColor_does_not_pass_on_a_colour_that_was_never_published()
    {
        var probe = new FakeProbe { Color = _ => null };

        var message = FailureOf(() => Expect(probe).Not.HasTextColor("#FFFFFF"));

        Assert.That(message, Does.Contain("not to have TextColor").And.Contain("<not published>"));
    }

    [Test]
    public void Not_HasColor_does_not_pass_on_a_typod_property_name()
    {
        var probe = new FakeProbe { Color = name => name == "TextColor" ? "#FF000000" : null };

        Assert.DoesNotThrow(() => Expect(probe).Not.HasColor("TextColor", "#FFFFFF"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.HasColor("TextColour", "#FFFFFF"));
    }

    [Test]
    public void Not_HasProperty_string_does_not_pass_on_a_property_that_was_never_published()
    {
        var probe = new FakeProbe { Property = name => name == "Value" ? "1" : null };

        Assert.DoesNotThrow(() => Expect(probe).Not.HasProperty("Value", "2"));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.HasProperty("Valu", "2"));
    }

    [Test]
    public void Not_HasProperty_int_does_not_pass_on_a_property_that_was_never_published()
    {
        var probe = new FakeProbe { Property = name => name == "Value" ? "1" : null };

        Assert.DoesNotThrow(() => Expect(probe).Not.HasProperty("Value", 2));
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.HasProperty("Valu", 2));
    }

    [Test]
    public void A_predicate_still_receives_null_so_it_can_assert_that_nothing_was_published()
    {
        var probe = new FakeProbe { Property = _ => null };

        Assert.DoesNotThrow(() => Expect(probe).HasProperty("Value", raw => raw is null, "be unpublished"));
    }

    [Test]
    public void An_unparseable_but_published_colour_is_still_judged_so_Not_passes()
    {
        // Published-but-wrong is a value to judge (it is not the colour), unlike never-published.
        Assert.DoesNotThrow(() => Expect(new FakeProbe { Color = _ => "blueish" }).Not.HasTextColor("#000"));
    }

    #endregion

    #region Chaining, timeouts, evidence

    [Test]
    public void Checks_chain_and_return_the_same_expectation()
    {
        var probe = new FakeProbe { Text = () => "120", Color = _ => "#FF7EB4F0", Property = _ => "9" };
        var expectation = Expect(probe);

        var returned = expectation.IsVisible().HasText("120").HasTextColor("#7EB4F0").HasProperty("Value", 9);

        Assert.That(returned, Is.SameAs(expectation));
    }

    [Test]
    public void A_chain_stops_at_the_first_failure()
    {
        var probe = new FakeProbe { Text = () => "wrong" };

        var message = FailureOf(() => Expect(probe).IsVisible().HasText("right").HasTextColor("#000"));

        Assert.That(message, Does.Contain("to have text 'right'"));
    }

    [Test]
    public void Not_negates_only_the_next_check()
    {
        var probe = new FakeProbe { Text = () => "a", Enabled = () => true };

        Assert.DoesNotThrow(() => Expect(probe).Not.HasText("b").IsEnabled());
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.HasText("b").Not.IsEnabled());
    }

    [Test]
    public void A_negated_check_returns_a_positive_expectation_so_later_checks_are_not_inverted()
    {
        var probe = new FakeProbe { Enabled = () => false, Toggled = () => false };

        Assert.DoesNotThrow(() => Expect(probe).Not.IsEnabled().Not.IsToggledOn());
        Assert.Throws<HarnessAssertionException>(() => Expect(probe).Not.IsEnabled().IsToggledOn());
    }

    [Test]
    public void Not_does_not_mutate_the_original()
    {
        var original = Expect(new FakeProbe { Text = () => "a" });

        _ = original.Not;

        Assert.DoesNotThrow(() => original.HasText("a"));
        Assert.That(original.Not.Not, Is.Not.SameAs(original));
        Assert.DoesNotThrow(() => original.Not.Not.HasText("a"));
    }

    [Test]
    public void WithTimeout_changes_how_long_the_check_waits()
    {
        var probe = new FakeProbe { Text = () => "no" };
        var slow = Expect(probe, Settings.Fast(timeoutMs: 5000));

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<HarnessAssertionException>(() => slow.WithTimeout(TimeSpan.FromMilliseconds(40)).HasText("yes"));

        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)));
    }

    [Test]
    public void WithTimeout_zero_is_a_single_look()
    {
        var probe = new FakeProbe { Text = () => "no" };

        Assert.Throws<HarnessAssertionException>(() => Expect(probe).WithTimeout(TimeSpan.Zero).HasText("yes"));

        Assert.That(probe.TextReads, Is.EqualTo(1));
    }

    [Test]
    public void WithTimeout_preserves_negation_and_rejects_a_negative_value()
    {
        var probe = new FakeProbe { Text = () => "a" };

        Assert.DoesNotThrow(() => Expect(probe).Not.WithTimeout(TimeSpan.FromMilliseconds(10)).HasText("b"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Expect(probe).WithTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Test]
    public void The_failure_message_reports_attempts_and_timeout()
    {
        var message = FailureOf(() => Expect(new FakeProbe { Text = () => "no" }).HasText("yes"));

        Assert.That(message, Does.Match(@"Gave up after [\d.,]+s \(\d+ attempts, timeout 0[.,]12s\)"));
    }

    [Test]
    public void The_failure_message_includes_a_screenshot_path_when_one_was_captured()
    {
        string? requested = null;
        var settings = Settings.Fast(screenshot: name => { requested = name; return "/tmp/shot.png"; });

        var message = FailureOf(() => Expect(new FakeProbe { Subject = "'xp'", Text = () => "no" }, settings).HasText("yes"));

        Assert.That(message, Does.Contain("Screenshot: /tmp/shot.png"));
        Assert.That(requested, Is.EqualTo("expect-failed-'xp'"));
    }

    [Test]
    public void No_screenshot_line_when_capture_returns_nothing()
    {
        var settings = Settings.Fast(screenshot: _ => null);

        var message = FailureOf(() => Expect(new FakeProbe { Text = () => "no" }, settings).HasText("yes"));

        Assert.That(message, Does.Not.Contain("Screenshot:"));
    }

    [Test]
    public void A_screenshot_that_throws_never_replaces_the_real_failure()
    {
        var settings = Settings.Fast(screenshot: _ => throw new InvalidOperationException("camera broke"));

        var message = FailureOf(() => Expect(new FakeProbe { Text = () => "no" }, settings).HasText("yes"));

        Assert.That(message, Does.Contain("Expected 'thing' to have text 'yes'"));
    }

    [Test]
    public void Successes_and_failures_are_logged()
    {
        var log = new List<string>();
        var settings = Settings.Fast(log: log.Add);

        Expect(new FakeProbe { Text = () => "a" }, settings).HasText("a");
        Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Text = () => "a" }, settings).HasText("b"));

        Assert.That(log, Has.Count.EqualTo(2));
        Assert.That(log[0], Does.Contain("✓").And.Contain("to have text 'a'"));
        Assert.That(log[1], Does.Contain("✗").And.Contain("to have text 'b'"));
    }

    [Test]
    public void Diagnostics_are_included_and_blank_diagnostics_are_not()
    {
        var withDiagnostics = FailureOf(() => Expect(new FakeProbe { Text = () => "no", DiagnosticsText = () => "Route: 'home'" }).HasText("yes"));
        var without = FailureOf(() => Expect(new FakeProbe { Text = () => "no", DiagnosticsText = () => "  " }).HasText("yes"));

        Assert.That(withDiagnostics, Does.Contain("Route: 'home'"));
        Assert.That(without.Split(Environment.NewLine), Has.Length.EqualTo(2));
    }

    [Test]
    public void The_failure_is_a_plain_exception_not_tied_to_any_test_framework()
    {
        var ex = Assert.Throws<HarnessAssertionException>(() => Expect(new FakeProbe { Text = () => "no" }).HasText("yes"));

        Assert.That(ex, Is.InstanceOf<Exception>());
        Assert.That(ex!.GetType().BaseType, Is.EqualTo(typeof(Exception)));
    }

    #endregion
}
