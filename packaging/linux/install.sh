#!/bin/sh
# Installs URL Shortener for the current user (no root needed):
#   ~/.local/bin/UrlShortener, an app-menu entry and its icon.
# Run from the unpacked release folder:  ./install.sh      Remove again:  ./install.sh --uninstall
set -eu

BIN="${XDG_BIN_HOME:-$HOME/.local/bin}"
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
APPS="$DATA/applications"
ICONS="$DATA/icons/hicolor/256x256/apps"
HERE="$(cd "$(dirname "$0")" && pwd)"

if [ "${1:-}" = "--uninstall" ]; then
    rm -f "$BIN/UrlShortener" "$APPS/url-shortener.desktop" "$ICONS/url-shortener.png"
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" || true
    echo "URL Shortener removed. Your history and settings are kept in ${XDG_CONFIG_HOME:-$HOME/.config}/UrlShortener."
    exit 0
fi

mkdir -p "$BIN" "$APPS" "$ICONS"
install -m 755 "$HERE/UrlShortener" "$BIN/UrlShortener"
install -m 644 "$HERE/url-shortener.png" "$ICONS/url-shortener.png"
# Point the menu entry at the installed binary, so it works even if ~/.local/bin isn't on PATH.
sed "s|^Exec=UrlShortener|Exec=$BIN/UrlShortener|" "$HERE/url-shortener.desktop" > "$APPS/url-shortener.desktop"
chmod 644 "$APPS/url-shortener.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q "$DATA/icons/hicolor" 2>/dev/null || true

echo "URL Shortener installed. Find it in your app menu, or run: $BIN/UrlShortener"
echo "Tip: give the command \"$BIN/UrlShortener --quick\" a keyboard shortcut to shorten the clipboard from anywhere."
