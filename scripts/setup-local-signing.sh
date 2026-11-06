#!/bin/bash
# Creates a persistent, local-only code signing identity. Never exports OTP data.
set -euo pipefail
NAME='PadakKey Local Development'
if security find-certificate -c "$NAME" >/dev/null 2>&1; then
  echo 'Local signing certificate already exists.'
  exit 0
fi
umask 077
TEMP_SIGNING_DIR="$(mktemp -d "${TMPDIR:-/tmp}/padakkey-signing.XXXXXX")"
trap 'rm -rf "$TEMP_SIGNING_DIR"' EXIT
cat > "$TEMP_SIGNING_DIR/cert.cnf" <<'CONF'
[req]
distinguished_name=dn
x509_extensions=extensions
prompt=no
[dn]
CN=PadakKey Local Development
[extensions]
basicConstraints=critical,CA:false
keyUsage=critical,digitalSignature
extendedKeyUsage=critical,codeSigning
subjectKeyIdentifier=hash
CONF
openssl rand -base64 32 > "$TEMP_SIGNING_DIR/password"
openssl req -new -newkey rsa:2048 -x509 -sha256 -days 3650 -nodes \
  -config "$TEMP_SIGNING_DIR/cert.cnf" -keyout "$TEMP_SIGNING_DIR/key.pem" -out "$TEMP_SIGNING_DIR/cert.pem" 2>/dev/null
openssl pkcs12 -export -name "$NAME" -inkey "$TEMP_SIGNING_DIR/key.pem" -in "$TEMP_SIGNING_DIR/cert.pem" \
  -out "$TEMP_SIGNING_DIR/identity.p12" -passout "file:$TEMP_SIGNING_DIR/password"
security import "$TEMP_SIGNING_DIR/identity.p12" -k "$HOME/Library/Keychains/login.keychain-db" \
  -P "$(cat "$TEMP_SIGNING_DIR/password")" -T /usr/bin/codesign
# No system trust settings are modified. Only codesign is granted key access.
echo 'Persistent local code signing identity installed in the login keychain.'
