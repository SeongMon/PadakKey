# Release workflow

The user requests a GitHub release for every delivered app update, not just a source push.

- Preserve unrelated changes from other computers; fetch and inspect before editing.
- After an app change, bump the affected platform version, run relevant checks and build, commit and push, and publish a new GitHub preview release with macOS and Windows ZIP assets plus SHA-256 checksums.
- If only one platform changed, reuse the latest successful build of the unchanged platform, verify its source matches, and state each platform's actual version in the release notes and README. Do not imply both platforms changed.
- Update README download links to the public release assets, not expiring Actions artifacts. Verify assets are uploaded and the release is not a draft before reporting completion.
- Use desktop-vVERSION tags for combined desktop releases; these do not trigger the legacy v* draft-release workflow.
- Local Mac builds must reuse the existing PadakKey Local Development signing identity. Never export/upload private keys or OTP data. Explain signing/notarization status in release notes.
- Do not commit or publish Patamon images. They were removed from GitHub at the user's request; local files outside the repository may be kept.
- Windows packages should expose only PadakKey.exe and Assets at the top level; retain the packaged-app self-test in CI.
