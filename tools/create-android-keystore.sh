#!/bin/sh
# Creates the Android release signing key and stores it as GitHub Actions secrets, so the release
# workflow can sign APKs. Run this once, on your own computer:
#
#   ./tools/create-android-keystore.sh
#
# Needs: keytool (comes with any JDK) and, for uploading the secrets, the GitHub CLI (gh) logged in.
#
# KEEP THE KEYSTORE FILE AND PASSWORD SAFE (e.g. in a password manager) AND NEVER COMMIT IT.
# Android only installs an update if it's signed with the same key as the installed app: if the key
# is lost, everyone has to uninstall and reinstall to get new versions.
set -eu

KEYSTORE="${1:-$HOME/url-shortener-release.keystore}"
ALIAS="urlshortener"
REPO="Bojacnik/URL-Shortener-app"

if [ -e "$KEYSTORE" ]; then
    echo "$KEYSTORE already exists; not overwriting it. Pass another path as the first argument if you really want a new key."
    exit 1
fi

printf "Choose a keystore password (at least 6 characters): "
stty -echo; read -r PASSWORD; stty echo; echo
printf "Repeat it: "
stty -echo; read -r AGAIN; stty echo; echo
[ "$PASSWORD" = "$AGAIN" ] || { echo "Passwords differ."; exit 1; }

# One password for both the store and the key keeps things simple (Android's PKCS12 keystores share it anyway).
keytool -genkeypair -v \
    -keystore "$KEYSTORE" -storetype PKCS12 \
    -alias "$ALIAS" -keyalg RSA -keysize 4096 -validity 10000 \
    -storepass "$PASSWORD" -keypass "$PASSWORD" \
    -dname "CN=URL Shortener, O=Bojacnik"
chmod 600 "$KEYSTORE"
echo
echo "Created $KEYSTORE — back it up now, together with the password."

if command -v gh >/dev/null 2>&1; then
    printf "Upload it as GitHub Actions secrets to %s now? [y/N] " "$REPO"
    read -r ANSWER
    if [ "$ANSWER" = "y" ] || [ "$ANSWER" = "Y" ]; then
        base64 -w0 "$KEYSTORE" | gh secret set ANDROID_KEYSTORE_BASE64 --repo "$REPO"
        printf %s "$PASSWORD" | gh secret set ANDROID_KEYSTORE_PASSWORD --repo "$REPO"
        printf %s "$PASSWORD" | gh secret set ANDROID_KEY_PASSWORD --repo "$REPO"
        printf %s "$ALIAS" | gh secret set ANDROID_KEY_ALIAS --repo "$REPO"
        echo "Done. Releases (git tag vX.Y.Z && git push origin vX.Y.Z) will now be signed with this key."
        exit 0
    fi
fi

echo
echo "To finish, add these four secrets in GitHub (Settings → Secrets and variables → Actions):"
echo "  ANDROID_KEYSTORE_BASE64   the output of:  base64 -w0 \"$KEYSTORE\""
echo "  ANDROID_KEYSTORE_PASSWORD your password"
echo "  ANDROID_KEY_PASSWORD      your password"
echo "  ANDROID_KEY_ALIAS         $ALIAS"
