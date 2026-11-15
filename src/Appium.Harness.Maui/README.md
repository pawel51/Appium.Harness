# Appium.Harness.Maui

The app-side half of [Appium.Harness](https://www.nuget.org/packages/Appium.Harness): it lets a
.NET MAUI app publish what an Appium test cannot otherwise see.

A native accessibility tree carries text, ids and a few state flags. It does **not** carry a MAUI
`Color`, or anything from a view model. So "the XP label is blue" or "the counter's bound value is
120" can only be tested by comparing pixels — unless the app publishes them. `TestProbe` does that,
into a field the tree does carry, and Appium.Harness reads it back:

```xml
<Label AutomationId="profile-total-xp" Text="{Binding TotalXp}" TextColor="#7EB4F0"
       probe:TestProbe.Properties="TextColor"
       probe:TestProbe.Value="{Binding TotalXp}" />
```

```csharp
Ui.Expect("profile-total-xp").HasTextColor("#7EB4F0").HasProperty("Value", 120);
```

## Setup

1. **Add the package to the app** (not the test project):

   ```sh
   dotnet add package Appium.Harness.Maui
   ```

2. **Turn it on, in test builds only**, in `MauiProgram.CreateMauiApp`:

   ```csharp
   using Appium.Harness.Maui;

   #if DEBUG
       builder.UseTestProbes();
   #endif
   ```

   Use whatever constant marks your test build. If your E2E run uses a Release build, define your
   own (`-p:DefineConstants=MYAPP_E2E`) rather than enabling probes in every Release build.

3. **Declare the namespace in XAML** and attach the probe to the views a test asserts on:

   ```xml
   xmlns:probe="clr-namespace:Appium.Harness.Maui;assembly=Appium.Harness.Maui"
   ```

   - `probe:TestProbe.Properties` — comma-separated names of public properties of the view, e.g.
     `"TextColor,BackgroundColor"`. They are republished whenever they change.
   - `probe:TestProbe.Value` — any value, usually a binding to a view-model property, published as
     `Value`.

## Why it is off by default

A published payload exposes colours and view-model values to anything that reads the accessibility
tree — assistive technology included. A shipping build must publish nothing.

The gate is a call the app makes, not a compile-time switch in this package, because a package is
compiled once, in Release, before your app decides what kind of build it is. In a build that never
calls `UseTestProbes()` the attached properties still exist, so the XAML compiles, and they do
nothing. A colour or `HasProperty` check against such a build fails with `<not published>`, which
is the correct answer.

## Where it lands

| | Field | Notes |
|---|---|---|
| Android | `contentDescription` | A `SemanticProperties.Description` is kept in front of the payload |
| iOS | `accessibilityValue` | The description stays in `accessibilityLabel`, untouched |

The value is a `[[probe:Name=value;…]]` payload. It is written by the same source file
Appium.Harness parses it with, so the two cannot drift.

**Do not probe a control whose iOS accessibility *value* a test already reads** — a `Switch` or an
`Entry` — since that is the field it uses there.

## Compatibility

Targets `net10.0-android` and `net10.0-ios`. Works with Appium.Harness 2.x.

Source, issues and changelog: https://github.com/pawel51/Appium.Harness
