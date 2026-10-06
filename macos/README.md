# TextPad for macOS

The native macOS edition of TextPad is implemented in Swift and AppKit.

**Current version:** 1.5.13  
**Minimum system:** macOS 11  
**Source:** `Sources/`  
**Application resources:** `Resources/`

## Build

Install the Xcode Command Line Tools, then run:

```bash
bash build.sh
```

The application is written to `TextPad.app`. To build and launch it:

```bash
bash run.sh
```

## Installers

```bash
bash installer/build-installer.sh
```

Outputs are written to `dist/` as a DMG and PKG named for the platform version and architecture. Set `REBUILD=1` to rebuild the app first, or `DMG_LAYOUT=1` for Finder-based DMG layout. Sign the application with `APP_SIGN_IDENTITY` (Developer ID Application). Sign the PKG with `INSTALLER_SIGN_IDENTITY` (Developer ID Installer). `SIGN_IDENTITY` still signs only the application.

## Versioning

The authoritative macOS version is `Resources/Info.plist`. A macOS-only release does not require changing the Windows version.

## Data locations

| Location | Purpose |
|---|---|
| `~/Library/Preferences/com.textpad.editor.plist` | Preferences |
| `~/Library/Application Support/com.textpad.editor/Autosave/` | Recovery snapshots |
| `~/Library/Application Support/com.textpad.editor/crash.log` | Crash log |

See [`CHANGELOG.md`](CHANGELOG.md) for macOS release history.
