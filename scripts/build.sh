#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION="${VERSION:-0.1.9}"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'VERSION must be x.y.z'; exit 1; }
ARCH="${ARCH:-universal}"
# Sign outside cloud/file-provider folders, which may inject Finder metadata.
STAGING="$(mktemp -d "${TMPDIR:-/tmp}/barotp-build.XXXXXX")"
trap 'rm -rf "$STAGING"' EXIT
APP="$STAGING/PadakKey.app"
mkdir -p "$PWD/dist"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
export CLANG_MODULE_CACHE_PATH="$PWD/.build/clang-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/.build/swift-cache"
BUILD_FLAGS=(--cache-path "$PWD/.build/cache")
if [[ "${BUILD_DISABLE_SANDBOX:-0}" == 1 ]]; then BUILD_FLAGS+=(--disable-sandbox); fi
if [[ "$ARCH" == universal ]]; then
  for TARGET in arm64 x86_64; do
    swift build "${BUILD_FLAGS[@]}" -c release --triple "$TARGET-apple-macosx13.0" --scratch-path ".build/$TARGET"
  done
  ARM_BIN="$(swift build "${BUILD_FLAGS[@]}" -c release --triple arm64-apple-macosx13.0 --scratch-path .build/arm64 --show-bin-path)/BarOTP"
  INTEL_BIN="$(swift build "${BUILD_FLAGS[@]}" -c release --triple x86_64-apple-macosx13.0 --scratch-path .build/x86_64 --show-bin-path)/BarOTP"
  lipo -create "$ARM_BIN" "$INTEL_BIN" -output "$APP/Contents/MacOS/BarOTP"
else
  swift build "${BUILD_FLAGS[@]}" -c release
  BIN="$(swift build "${BUILD_FLAGS[@]}" -c release --show-bin-path)/BarOTP"
  cp "$BIN" "$APP/Contents/MacOS/BarOTP"
fi
cp Sources/BarOTP/Resources/*.png "$APP/Contents/Resources/"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>BarOTP</string>
<key>CFBundleIdentifier</key><string>app.barotp.mac</string>
<key>CFBundleName</key><string>PadakKey</string>
<key>CFBundleDisplayName</key><string>PadakKey</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$VERSION</string>
<key>CFBundleVersion</key><string>$VERSION</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
xattr -cr "$APP"
if [[ -n "${SIGNING_IDENTITY:-}" ]]; then
  codesign --force --options runtime --timestamp --sign "$SIGNING_IDENTITY" "$APP"
else
  codesign --force --sign - "$APP"
fi
codesign --verify --deep --strict "$APP"
ZIP="$PWD/dist/PadakKey-$VERSION-$ARCH.zip"
rm -f "$ZIP"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ZIP"
if [[ -n "${NOTARY_PROFILE:-}" ]]; then
  [[ -n "${SIGNING_IDENTITY:-}" ]] || { echo 'Notarization requires SIGNING_IDENTITY'; exit 1; }
  xcrun notarytool submit "$ZIP" --keychain-profile "$NOTARY_PROFILE" --wait
  xcrun stapler staple "$APP"
  xcrun stapler validate "$APP"
  rm -f "$ZIP"
  ditto -c -k --sequesterRsrc --keepParent "$APP" "$ZIP"
fi
(cd dist && shasum -a 256 "$(basename "$ZIP")" > "$(basename "$ZIP").sha256")
ditto "$APP" "$PWD/dist/PadakKey.app"
echo "$ZIP"
