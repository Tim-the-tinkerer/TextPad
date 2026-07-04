# TextPad

A lightweight text editor for **macOS** and **Windows**, inspired by BBEdit and CotEditor. Both platforms share the same feature set: plain-text and rich-text editing, syntax highlighting, multi-tab workflows, large-file handling, and auto-save recovery.

**Current version: 1.5.2**

| Platform | Source | Technology |
|----------|--------|------------|
| macOS | `macos/` | Swift, AppKit |
| Windows | `windows-x64/` | C#, WPF (.NET 8) |

---

## Features

- Plain text and RTF rich-text editing
- Syntax highlighting (Swift, Python, JavaScript, HTML, CSS, JSON, Markdown, Shell, C/C++, and more)
- Multi-tab editing with reopen-closed-tab
- Find, replace, and go-to-line
- Line numbers, word wrap, invisible characters, current-line highlight
- Themes: Light, Dark, Solarized, Sepia, and System
- Character encoding options (UTF-8, UTF-16, Latin-1, and others)
- Line-ending detection and conversion (LF / CRLF / CR)
- Auto-save recovery snapshots
- External file-change detection
- PDF and HTML export
- Single-instance behavior (additional launches open files in the running app)
- Built-in help (**F1**)

---

## Project layout

```
TextPad/
├── README.md              ← this file
├── build.sh               ← macOS build wrapper
├── run.sh                 ← macOS build + launch
├── macos/                 ← macOS app source
│   ├── build.sh
│   ├── run.sh
│   ├── Help.md
│   ├── CHANGELOG.md
│   └── installer/
│       └── build-installer.sh
└── windows-x64/           ← Windows app source
    ├── build.ps1
    ├── build_installer.ps1
    ├── CHANGELOG.md
    ├── TextPad/
    └── installer/
        └── TextPad.iss
```

---

## macOS

### Requirements

- macOS 11.0 or later
- Xcode Command Line Tools (`xcode-select --install`)

### Build

```bash
cd macos
bash build.sh
```

Or from the repo root:

```bash
bash build.sh        # build only
bash run.sh          # build and open TextPad.app
```

The app bundle is created at `macos/TextPad.app`.

### Installers

```bash
cd macos
bash installer/build-installer.sh
```

Produces in `macos/dist/`:

| File | Description |
|------|-------------|
| `TextPad-1.5.2-mac-arm64.dmg` | Drag-to-Applications disk image |
| `TextPad-1.5.2-mac-arm64.pkg` | Guided installer (copies to `/Applications`) |

On Intel Macs the architecture label is `x64` instead of `arm64`.

**Options:**

```bash
REBUILD=1 bash installer/build-installer.sh          # rebuild app first
DMG_LAYOUT=1 bash installer/build-installer.sh     # customize DMG icon layout
SIGN_IDENTITY="Developer ID Application: …" bash installer/build-installer.sh
```

Unsigned builds are ad-hoc signed for local use. For distribution, sign with a Developer ID certificate and notarize with Apple.

### Data files (macOS)

| Location | Purpose |
|----------|---------|
| `~/Library/Preferences/com.textpad.editor.plist` | Preferences |
| `~/Library/Application Support/com.textpad.editor/Autosave/` | Recovery snapshots |
| `~/Library/Application Support/com.textpad.editor/crash.log` | Error log |

---

## Windows

### Requirements

- Windows 10 or later (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Inno Setup 6 (for the installer; downloaded automatically by the build script if missing)

### Build

```powershell
cd windows-x64
.\build.ps1
```

Output: `windows-x64\dist\x64\TextPad.exe`

### Installer

```powershell
cd windows-x64
.\build_installer.ps1
```

Produces: `windows-x64\dist\TextPad-1.5.2-win-x64-Setup.exe`

Pass `-SkipSign` to skip code signing when no certificate is configured.

### Data files (Windows)

| Location | Purpose |
|----------|---------|
| `%APPDATA%\com.textpad.editor\settings.json` | Preferences and recent files |
| `%APPDATA%\com.textpad.editor\Autosave\` | Recovery snapshots |
| `%APPDATA%\com.textpad.editor\crash.log` | Error log |

---

## Help

Both platforms ship a built-in `Help.md`. Open it from **Help → TextPad Help** or press **F1**.

Source copies:

- macOS: `macos/Help.md`
- Windows: `windows-x64/TextPad/Assets/Help.md`

---

## Changelog

Platform-specific release notes:

- [macOS CHANGELOG](macos/CHANGELOG.md)
- [Windows CHANGELOG](windows-x64/CHANGELOG.md)

---

## License

Copyright © 2026. All rights reserved.