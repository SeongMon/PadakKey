#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build/verify-vault
export CLANG_MODULE_CACHE_PATH="$PWD/.build/clang-cache"
swiftc -emit-library -emit-module -module-name OTPCore Sources/OTPCore/OTP.swift -emit-module-path .build/verify-vault/OTPCore.swiftmodule -o .build/verify-vault/libOTPCore.dylib
swiftc -I .build/verify-vault -L .build/verify-vault -lOTPCore -Xlinker -rpath -Xlinker "$PWD/.build/verify-vault" Sources/BarOTP/Vault.swift Tests/VaultChecks/VaultChecks.swift -o .build/verify-vault/vault-checks
.build/verify-vault/vault-checks
