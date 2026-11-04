#!/bin/bash
# Run the same core test cases when Command Line Tools lacks XCTest.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build/verify
export CLANG_MODULE_CACHE_PATH="$PWD/.build/clang-cache"
swiftc -D STANDALONE Sources/OTPCore/OTP.swift Tests/OTPCoreTests/OTPTests.swift -o .build/verify/otp-verify
.build/verify/otp-verify
