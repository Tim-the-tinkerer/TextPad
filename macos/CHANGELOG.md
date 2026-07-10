# TextPad (macOS) Changelog

## 1.5.3 — 2026-07-10

Version aligned with Windows 1.5.3.

### Fixed
- App icon missing in Finder after install: `AppIcon.icns` is now packaged with world-readable permissions.
- RTF receipts and other email-style documents with light gray text on white table cells now render with readable contrast in all themes.
- RTF table layout from Mail and similar sources is preserved when opening rich-text documents.
- Plain-text files with unrecognized encodings (including some binary and Synology `.enclave` key files) open with an ISO Latin-1 fallback instead of failing with “Unable to decode file encoding.”
- The Open dialog accepts all file types, not only common text UTIs.

## 1.5.2 — 2026-07-04

Version aligned with Windows 1.5.2.

### Added
- Built-in help file (`Help.md`) — open from **Help → TextPad Help** or press **F1**.
- macOS installers (DMG and PKG) via `bash installer/build-installer.sh`; output in `macos/dist/`.

### Fixed
- Text selection was hard to see on single-line documents and across all themes; selection colors are now stronger and distinct from the current-line highlight.
- Current-line highlight no longer fills the entire editor width on short lines, and it skips over selected text so selection remains visible.
- Syntax highlighting could flicker or show stale colors while typing; concurrent highlight jobs are now cancelled correctly and attribute updates no longer retrigger highlighting.
- Swift `#selector` / `#if` directives and C/C++ `#include` lines were incorrectly colored as comments.
- JSON property keys were colored as strings instead of keys.
- `build.sh` failed when run from another directory, when scripts lacked the executable bit, or when icon generation ran on Google Drive (`sips` temp-file errors).

### Changed
- Current-line highlight uses a subtle row tint plus a left accent bar instead of a solid full-width band.
- Build scripts always `cd` to their own directory and invoke via `bash` for reliability.

## 1.4.0

### Fixed
- Large files scroll to the end: the text view grows vertically and horizontally instead of being capped at a fixed size.
- Word wrap works again after toggling; re-enabling wrap resets the text container width.
- Very long lines (>8,000 characters) auto-disable word wrap on initial open only.
- Replace All no longer corrupts text when the replacement length differs from the search string.
- File-change monitor debounces events and suppresses false prompts after saving.
- "Keep Current Version" marks the document dirty so a later Save does not silently overwrite disk.
- Go to Line handles CRLF and CR line endings correctly.
- Syntax highlighter no longer crashes on unclosed string literals at column 0.
- Syntax highlighting is skipped for documents over 500,000 characters.
- Safe file reads retry until the on-disk file size is stable.
- Files over 100 MB are rejected with a clear error instead of hanging or crashing.
- Encoding dialog guards against invalid popup selections.
- Single-instance server fails gracefully when the IPC socket cannot listen.

### Added
- `SafeFileReader` for stable reads of files being written by other apps.
- RTF data preserved in auto-save snapshots (`rtfDataBase64`).

### Changed
- Auto-save timer runs in the common run-loop mode (fires during scroll) and restarts when preferences change.
- UTF-16 saves include the correct byte-order mark.
- Notification observers are removed on editor teardown.

## 1.3.0

### Added
- Single-instance behavior: launching TextPad again forwards file paths to the running app.
- Crash logging to `~/Library/Application Support/com.textpad.editor/crash.log`.
- Plain-text PDF and HTML export (previously rich-text only).

### Changed
- Version aligned with Windows 1.3.0; About panel reads version from Info.plist.

## 1.2.0 — 2026 Audit

### Fixed / Cleaned
- Eliminated "variable 'options' was never mutated" warning in FindReplaceController (changed `var` to `let`).
- Resolved "use '#selector'" compiler warnings in AppDelegate for rich text menu forwarding actions (switched to `NSSelectorFromString` for dynamic responder actions).
- Full rebuild produces zero warnings.

No user-visible behavior changes in this pass.