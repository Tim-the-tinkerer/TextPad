# TextPad for Windows

The native Windows edition of TextPad is implemented in C#, WPF and .NET 8. AvalonEdit provides the plain-text editing surface.

**Current version:** 1.5.5  
**Minimum system:** Windows 10 x64  
**Project:** `TextPad/TextPad.csproj`

## Build

Install the .NET 8 SDK, then run in PowerShell:

```powershell
.\build.ps1
```

The framework-dependent application is written to `dist\x64\`.

## Installer

```powershell
.\build_installer.ps1
```

This publishes a self-contained x64 application and creates `dist\TextPad-<version>-win-x64-Setup.exe`. Inno Setup is downloaded when it is not already installed. Pass `-SkipSign` when no signing certificate is configured.

## Versioning

The authoritative Windows version is the `<Version>` value in `TextPad/TextPad.csproj`. A Windows-only release does not require changing the macOS version.

## Data locations

| Location | Purpose |
|---|---|
| `%APPDATA%\com.textpad.editor\settings.json` | Preferences and recent files |
| `%APPDATA%\com.textpad.editor\Autosave\` | Recovery snapshots |
| `%APPDATA%\com.textpad.editor\crash.log` | Crash log |

See [`CHANGELOG.md`](CHANGELOG.md) for Windows release history.
