# URL Shortener

[![CI](https://github.com/Bojacnik/URL-Shortener-app/actions/workflows/ci.yml/badge.svg)](https://github.com/Bojacnik/URL-Shortener-app/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A link shortener for **Windows, Linux, macOS and Android**, built with [Avalonia UI](https://avaloniaui.net/) and .NET 10. The same UI runs on all of them.

<p>
  <img src="docs/desktop-light.png" alt="Desktop, light theme" width="420" />
  <img src="docs/phone-dark.png" alt="Android phone, dark theme" width="300" />
</p>

## Features

- **Four free public services, with no account or API key:** [is.gd](https://is.gd) (the default), [TinyURL](https://tinyurl.com), [da.gd](https://da.gd) and [v.gd](https://v.gd).
- **Automatic fallback:** if the chosen service is down or rate-limiting, the next one is tried. Your own mistakes, such as an invalid URL or an alias that's taken, don't trigger a fallback.
- **Custom aliases** (`is.gd/my_link`), checked against each service's rules as you type.
- **Live validation:** `example.com` works without `https://`. Local and private-network addresses are rejected.
- **One-tap actions:** Copy (automatic by default; `Ctrl+Shift+C` on desktop), Open, Share (Android), and a QR code.
- **Searchable recent links:** tap a row to copy the link again. History is stored only on this device.
- **Light, dark or system theme**, with small animations: cards fade in, the Copy button confirms "Copied", and a toast appears for actions.
- **Android Share menu:** in Chrome, YouTube or any other app, choose *Share → URL Shortener* and the link is shortened right away.

History and settings are saved as JSON in the per-user app data folder (`~/.config/UrlShortener` on Linux, `%APPDATA%\UrlShortener` on Windows, and app-private storage on Android).

## Download

Get the latest version from the [**Releases**](https://github.com/Bojacnik/URL-Shortener-app/releases) page:

| Platform | File |
| --- | --- |
| Windows | `UrlShortener-vX.Y.Z-win-x64.zip` (contains `UrlShortener.exe`) |
| Linux | `UrlShortener-vX.Y.Z-linux-x64.tar.gz` (a single executable) |
| macOS (Apple Silicon) | `UrlShortener-vX.Y.Z-osx-arm64.tar.gz` |
| Android 6.0+ | `UrlShortener-vX.Y.Z-android.apk` (for 64-bit ARM and x86 devices; allow "install unknown apps" to sideload) |

Every push also builds these, available as artifacts under **Actions**.

## Build it yourself

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Desktop: run
dotnet run --project src/UrlShortener.Desktop

# Desktop: single-file build (swap linux-x64 for win-x64 or osx-arm64)
dotnet publish src/UrlShortener.Desktop -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish

# Android: needs the workload, a JDK (17+) and the Android SDK
dotnet workload install android
dotnet publish src/UrlShortener.Android -c Release -p:AndroidSdkDirectory=$ANDROID_HOME -o publish
adb install publish/com.bojacnik.urlshortener-Signed.apk
```

The Android build is signed with the debug key, which is fine for sideloading. Publishing to Google Play needs your own keystore (`AndroidSigningKeyStore` and related properties).

## Test

```bash
dotnet test tests/UrlShortener.Tests
```

There are 70 tests:
- **Providers:** run against a fake HTTP handler, so no network is needed.
- **Logic and storage:** fallback, validation, storage, and the view model.
- **Headless UI tests:** they drive the real views (type a URL, press Enter, check the result) and render desktop and phone-sized screenshots. Set `SCREENSHOT_DIR` to keep the screenshots.

## Project layout

| Project | What's in it |
| --- | --- |
| `src/UrlShortener.Core` | Providers, `ShortenerService` (fallback), URL validation, JSON storage. No UI. |
| `src/UrlShortener.UI` | Shared Avalonia UI: `MainView`, view models, styles, icons, animations. |
| `src/UrlShortener.Desktop` | Desktop app (Windows, Linux, macOS). |
| `src/UrlShortener.Android` | Android app: activity, Share intent, icons, splash screen. |
| `tests/UrlShortener.Tests` | xUnit v3 + Avalonia.Headless. |

Repo-wide files:
- `Directory.Packages.props`: one version per NuGet package.
- `Directory.Build.props`: settings shared by all projects (language, version, metadata; warnings become errors in CI).
- `global.json`: pins the .NET 10 SDK.
- `.editorconfig`: code style, which CI checks with `dotnet format`.
- `.gitattributes`: line endings.
- `.github/dependabot.yml`: weekly dependency updates.

### Adding a service

Subclass `ShortenerProviderBase`. Implement `BuildRequestUri` and `ParseResponse`, set the alias rules, then register the class in `ShortenerService.CreateDefault`.

## Releasing

1. Move the `[Unreleased]` notes in [CHANGELOG.md](CHANGELOG.md) under a new version heading.
2. Tag and push: `git tag v2.2.0 && git push origin v2.2.0`.

The [release workflow](.github/workflows/release.yml) tests the code, builds every platform with the version from the tag, and publishes a GitHub Release. The release notes are taken from the changelog.

## History

See [CHANGELOG.md](CHANGELOG.md). Version 1 was a WinForms app for .NET Framework 4.8 that used the CleanURI API, which has since shut down.

## License

[MIT](LICENSE) © 2025-2026 Bojacnik
