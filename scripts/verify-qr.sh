#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build/verify
export CLANG_MODULE_CACHE_PATH="$PWD/.build/clang-cache"
swiftc -D STANDALONE Sources/OTPCore/OTP.swift Sources/BarOTP/QRReader.swift Tests/QRImportChecks/QRImportChecks.swift -o .build/verify/qr-verify
.build/verify/qr-verify
