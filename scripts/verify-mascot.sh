#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build/verify-mascot/Assets.bundle/Contents/Resources
export CLANG_MODULE_CACHE_PATH="$PWD/.build/clang-cache"
cp Sources/BarOTP/Resources/Sparrow*.png .build/verify-mascot/Assets.bundle/Contents/Resources/
cat > .build/verify-mascot/Assets.bundle/Contents/Info.plist <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?><plist version="1.0"><dict><key>CFBundleIdentifier</key><string>app.padakkey.mascotchecks</string><key>CFBundlePackageType</key><string>BNDL</string></dict></plist>
PLIST
swiftc Sources/BarOTP/MenuMascot.swift Tests/MascotChecks/MascotChecks.swift -o .build/verify-mascot/mascot-checks
.build/verify-mascot/mascot-checks "$PWD/.build/verify-mascot/Assets.bundle"
