#!/bin/bash
# Verify an update remains the same Keychain client without reading any secrets.
set -euo pipefail
cd "$(dirname "$0")/.."
IDENTITY="${SIGNING_IDENTITY:-$(security find-certificate -c 'PadakKey Local Development' -Z 2>/dev/null | awk '/SHA-1 hash:/ {print $3; exit}')}"
[[ -n "$IDENTITY" ]] || { echo 'A persistent signing identity is required.' >&2; exit 1; }
CHECK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/padakkey-signing-check.XXXXXX")"
trap 'rm -rf "$CHECK_DIR"' EXIT
ditto dist/PadakKey.app "$CHECK_DIR/Update.app"
xattr -cr "$CHECK_DIR/Update.app"
codesign -d -r- dist/PadakKey.app > "$CHECK_DIR/before" 2>/dev/null
/usr/libexec/PlistBuddy -c 'Set :CFBundleVersion 999' "$CHECK_DIR/Update.app/Contents/Info.plist"
codesign --force --timestamp=none --sign "$IDENTITY" "$CHECK_DIR/Update.app"
codesign --verify --deep --strict "$CHECK_DIR/Update.app"
codesign -d -r- "$CHECK_DIR/Update.app" > "$CHECK_DIR/after" 2>/dev/null
cmp "$CHECK_DIR/before" "$CHECK_DIR/after"
grep -q 'certificate' "$CHECK_DIR/after"
echo 'PASS: changed app version retains the same certificate-based designated requirement.'
