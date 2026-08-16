# Releasing TextPad

macOS and Windows releases are independent even when their version numbers happen to match.

## macOS release

1. Update `macos/Resources/Info.plist`.
2. Add an entry to `macos/CHANGELOG.md`.
3. Update the macOS row in the root README.
4. Build and test from `macos/`.
5. Create the installers with `macos/installer/build-installer.sh`.
6. Tag the commit as `macos-v<version>`.
7. Attach only the macOS DMG and PKG to that GitHub release.

## Windows release

1. Update `<Version>` in `windows/TextPad/TextPad.csproj`.
2. Add an entry to `windows/CHANGELOG.md`.
3. Update the Windows row in the root README.
4. Build and test from `windows/`.
5. Create the installer with `windows/build_installer.ps1`.
6. Tag the commit as `windows-v<version>`.
7. Attach only the Windows Setup executable to that GitHub release.

## Cross-platform change

Commit the shared contract or fixture change once, then release each edition only after its implementation is ready. The releases may occur at different times and use different version numbers.
