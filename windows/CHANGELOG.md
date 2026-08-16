# TextPad (Windows) Changelog

## 1.5.5 — 2026-08-16

### Added
- Bundled **Interlac** and **Interlac Unicode** fonts. They appear in Preferences and are registered so RTF documents that name them render without a system install.

### Changed
- Long tab titles already truncate; the tab strip now scrolls horizontally when documents overflow the window. Hovering a tab shows the full file name.

### Fixed
- RTF documents keep the fonts named in their font table. Preferences no longer replace those faces with Segoe UI, and named families that WPF dropped (including `\fnil` entries such as Interlac Unicode) are restored after open.
- Near-white Cocoa RTF text (for example `#F0F2EB` on a white page) is lifted to readable black ink instead of disappearing into the paper. Saturated colors and already-readable grays are left alone.

## 1.5.4 — 2026-08-16

### Fixed
- Large and single-line documents remain in AvalonEdit instead of switching to a second WPF TextBox editor.
- Extremely long lines no longer force word wrapping.
- UTF-8 validation covers the complete file and cannot fail merely because a sample ended inside a multibyte character.
- BOM-less UTF-16 LE and BE files are detected using byte-position evidence.
- Text encoders and decoders use strict fallbacks; unsupported characters now produce a clear error instead of silently becoming question marks.
- Mac Roman is available alongside the existing legacy encodings.
- Rich text is shown on a neutral paper surface; application themes no longer rewrite document colors during ordinary viewing.
- The explicit file-size ceiling is raised from 100 MB to 256 MB.

## 1.5.3 — 2026-07-10

Version aligned with macOS 1.5.3.

### Fixed
- RTF receipts and other email-style documents with light gray text on white table cells now render with readable contrast in all themes, including content inside table cells.
- Cocoa/email RTF documents with nested tables (e.g. FastSpring receipts saved from macOS TextEdit) no longer render as a broken side-by-side layout. Nested tables are flattened into a readable top-to-bottom flow; simple two-column rows such as product | price are preserved.
- Near-black body text with a slight color bias (common in Cocoa RTF, e.g. `#0D0D12`) is remapped for contrast on dark themes instead of being treated as an intentional accent.
- Transparent table cells no longer assume a white page background when remapping text colors, which left receipt body text unreadable on dark themes.
- Hyperlinks keep the theme accent color; nested RTF span colors no longer override it. Low-contrast saturated accents (such as price reds) are brightened on dark themes.
- Export as PDF for plain text now embeds real fonts (vector text) instead of a soft 96‑DPI page screenshot, so text stays sharp when zoomed or printed. Long lines wrap to the page width.
- Export as PDF for rich text renders pages at 300 DPI instead of 96 DPI, reducing blur and distortion.

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
