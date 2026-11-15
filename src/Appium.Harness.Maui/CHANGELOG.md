# Changelog — Appium.Harness.Maui

All notable changes to this package. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the version numbers follow
[Semantic Versioning](https://semver.org/).

## [1.0.0] — 2026-11-15

First release. `TestProbe` comes from the AlohaAI sample app, where it was app source.

### Added

- `TestProbe` with the `Properties` and `Value` attached properties, publishing a probe payload
  into Android's `contentDescription` and iOS's `accessibilityValue`.
- `UseTestProbes()` on `MauiAppBuilder`, and `TestProbe.IsEnabled`.

### Changed (from the sample app's version)

- **Off until `UseTestProbes()` is called.** The app version was gated by
  `#if DEBUG || ALOHA_WEB_INSPECTABLE`, which cannot work in a package: it is compiled once, in
  Release. The app now opts in from its own test-build `#if`.
