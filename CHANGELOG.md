# Changelog

All notable changes to this project are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- MIT license, `global.json`, `Directory.Build.props`, `.editorconfig` and `.gitattributes`.
- Dependabot updates for NuGet packages (Avalonia packages grouped together) and GitHub Actions.
- Release workflow: pushing a `v*` tag publishes a GitHub Release with the Windows, Linux, macOS and Android builds attached.

## [2.1.0] - 2026-11-16

### Added
- Android app (Android 6.0+).
- "Share → URL Shortener" from any Android app shortens the shared link immediately.
- Share button for the result and for saved links on Android.
- QR code toggle, toast notifications, and a "Copied" confirmation on the Copy button.
- Settings flyout: theme, automatic fallback, automatic copy.
- Search in recent links once there are more than 3.

### Changed
- Complete visual redesign: indigo–violet palette in light and dark themes, new icons, a pill-shaped service picker, a gradient Shorten button with a progress spinner, and subtle entrance animations.
- The project is split into Core, shared UI, Desktop and Android projects, with central package versions.
- The Shorten button stays disabled while the URL or alias is invalid.

## [2.0.0] - 2026-11-15

### Changed
- Rewritten from WinForms (.NET Framework 4.8) to Avalonia 12 on .NET 10. Runs on Windows, Linux and macOS.

### Added
- Four free services with no API key: is.gd, TinyURL, da.gd and v.gd.
- Automatic fallback to another service when one is down or rate-limiting.
- Custom aliases, live URL validation, open in browser, and persistent history.
- Unit tests and headless UI tests, and GitHub Actions builds for every platform.

### Removed
- CleanURI, which has shut down.

### Fixed
- is.gd responses failing to decode because of a non-standard `charset=utf8` header.

## [1.0.0]

### Added
- First version: a WinForms app that shortened links through the CleanURI API.

[Unreleased]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.1.0...HEAD
[2.1.0]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/Bojacnik/URL-Shortener-app/releases/tag/v2.0.0
[1.0.0]: https://github.com/Bojacnik/URL-Shortener-app/commits/master
