# Changelog

All notable changes to this project are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [2.3.0] - 2026-11-16

### Added
- **Pin links:** pinned links stay at the top of the history and survive "Clear all".
- **Export and import history** as CSV, for backups or moving to another device. The file opens in Excel, LibreOffice and Google Sheets.
- **Save the QR code** as a PNG image.
- **Desktop clipboard suggestion:** when the window gets focus and a long link is on the clipboard, the app offers to shorten it with one click. Each link is offered only once.
- **Update notice:** at most once a day, the app asks GitHub Releases whether a newer version exists. "Later" hides that version, and the check can be turned off in Settings.
- **Android "Shorten link" in the text-selection menu:** select a link in any app and tap *Shorten link*. In editable fields the link is replaced with the short one; elsewhere the short link is copied.
- **Android Quick Settings tile and launcher shortcut** (long-press the icon) that shorten the link on the clipboard.
- Android launcher and menu labels in all 18 languages.
- **Linux:** an app-menu entry (with a "Shorten clipboard" action), an icon, an `install.sh` per-user installer, and an AppImage.

### Changed
- **Smaller downloads:** desktop builds are trimmed and compressed (Linux: 100 MB → 24 MB), and Android releases ship one APK per CPU type (arm64: 47 MB → 25 MB).
- **Release signing:** releases are signed with your own Android key from GitHub secrets, and the release workflow refuses to run without it. `tools/create-android-keystore.sh` sets the key up.
- **Accessibility:** colours meet WCAG AA contrast in both themes, and every icon-only button has a screen-reader name.

## [2.2.2] - 2026-11-16

### Fixed
- Android: the gap under the status bar got bigger in 2.2.1. Avalonia already keeps the app clear of the system bars, so the app no longer adds its own safe-area padding at all. The status bar now takes the app's background colour, with readable icons in both themes.

## [2.2.1] - 2026-11-16

### Fixed
- Android: an empty band under the status bar. The app now draws edge to edge (the background continues behind the status and navigation bars), pads its content by the system bars only once, and keeps the status-bar icons readable in both light and dark themes.

## [2.2.0] - 2026-11-16

### Added
- **18 languages:** English, Spanish, Czech, German, French, Italian, Portuguese (Brazil), Polish, Dutch, Russian, Ukrainian, Turkish, Indonesian, Arabic (right-to-left), Hindi, Chinese (Simplified), Japanese and Korean. The app follows the system language, and you can change it in Settings without restarting.
- **Expand mode:** see where a short link really goes before opening it. Redirects are followed one hop at a time without loading pages, the full path is shown, and following stops before any local or private network address.
- **Batch mode:** shorten up to 50 links at once. Results can be copied as a list or as a two-column table for spreadsheets.
- **Short-link detection:** pasting a link that is already short (bit.ly, t.co, youtu.be …) offers to expand it, and sharing one to the app on Android expands it automatically.
- **Desktop quick mode:** `UrlShortener --quick` shortens the link on the clipboard in a small pop-up and copies the result. Bind it to a keyboard shortcut in your system settings.
- **Command line:** `UrlShortener shorten <url> [--service …] [--alias …]` prints a short link, for scripts.
- Every release file comes with a `SHA256SUMS` entry and a signed build attestation (Sigstore), which you can check with `gh attestation verify`.
- MIT license, `global.json`, `Directory.Build.props`, `.editorconfig`, `.gitattributes`, Dependabot, and a release workflow that publishes a GitHub Release when a `v*` tag is pushed.

### Changed
- A segmented Shorten / Batch / Expand switcher at the top of the app.
- Error messages are now short translated explanations instead of the services' own English text.

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

[Unreleased]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.3.0...HEAD
[2.3.0]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.2.2...v2.3.0
[2.2.2]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.2.1...v2.2.2
[2.2.1]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.2.0...v2.2.1
[2.2.0]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/Bojacnik/URL-Shortener-app/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/Bojacnik/URL-Shortener-app/releases/tag/v2.0.0
[1.0.0]: https://github.com/Bojacnik/URL-Shortener-app/commits/master
