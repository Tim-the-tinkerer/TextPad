# TextPad (Windows) Changelog

## 1.5.2 — 2026-07-04

### Added
- Right-click context menu in the editor with undo, clipboard, find, and go-to-line commands.

### Fixed
- Text selection highlight was nearly invisible on dark and Solarized themes; all editors now use the theme selection color.
- Copy, cut, paste, and select-all shortcuts now respect focus (e.g. the find bar vs. the document editor).
- Large-file editor line-number measurement no longer clears the selection mid-copy.
- Context menu separators no longer show white notches on the left edge.

## 1.5.1 — 2026-06-27

### Added
- Built-in help file (`Help.md`) — open from **Help → TextPad Help** or press **F1**.

### Fixed
- Syntax highlighting on dark and Solarized themes: Markdown headings, links, block quotes, and other low-contrast tokens are now readable.
- Dark-theme chrome: menu bar, status bar, and find bar now follow the active theme colors.
- Installer build failed in Inno Setup due to nested `{#define}` constants in `TextPad.iss`.
- File menu dropdown text unreadable on dark themes (light text on white popup background).

## 1.5.0 — 2026-06-27

### Changed
- Windows builds are **64-bit only**; 32-bit (x86) support has been removed.
- Project directory renamed from `windows-x86` to `windows-x64`.
- Default publish output is `dist\x64`; installer is `TextPad-{version}-win-x64-Setup.exe`.
- Installer uses 64-bit Program Files (`{autopf}`) and requires a 64-bit edition of Windows.

## 1.4.0 — 2026-06-27

### Fixed
- Large single-line files (multi-MB JSON, vault exports, etc.) no longer freeze or lock up the editor on open.
- Extremely long line detection scans every line in the file instead of stopping at the first newline.
- "There are no open undo groups" crash on startup.
- `TextDocument` thread-ownership error when opening files asynchronously.
- Opening a file on the default untitled tab no longer closes the application.
- Window shutdown prompts for all dirty tabs before disposing any tab.
- `FileChangeMonitor` no longer touches `DispatcherTimer` from watcher background threads.
- Failed file open removes the broken loading tab instead of leaving an empty editor.
- Duplicate tabs when opening the same path from multiple sources concurrently.
- Find Next no longer skips the first match after a fresh search.
- Find-while-typing on large documents is debounced to avoid UI stalls.
- RTF status bar character count no longer materializes the full plain-text document on every keystroke.
- Auto-save no longer snapshots tabs after they have been disposed.
- Corrupt RTF auto-save/recovery data shows a clear error instead of crashing.
- Externally deleted files are marked "(missing)" in the tab title with guidance to use Save As.
- Line numbers in the large-file editor align correctly while scrolling.
- Line numbers remain available on large files that have a modest number of logical lines.

### Added
- `SimplePlainTextEditor` — lightweight wrapped-text fallback for files with extremely long lines (≥500 KB).
- Synchronized line-number gutter for the large-file editor.
- `PlainTextOpenPayload` for background file preparation and editor handoff.
- Sequential `OpenFileAsync` with in-flight path tracking.

### Changed
- Plain-text files load on a background thread; the tab appears immediately with a loading title.
- Auto-save RTF comparison uses raw bytes instead of parsing RTF into a throwaway control.
- Recoverable UI exceptions narrowed to I/O and format errors (no longer swallows all `InvalidOperationException`).
- About dialog reads the version from the assembly.

## 1.3.0

### Added
- Single-instance behavior: a second launch forwards file paths to the running app.
- Crash logging to `%APPDATA%\com.textpad.editor\crash.log`.
- Plain-text PDF and HTML export.
- Auto-save and recovery.
- Syntax highlighting, find/replace, external file change detection.

### Changed
- Version aligned with macOS 1.3.0.