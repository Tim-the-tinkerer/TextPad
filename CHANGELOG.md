# Repository changelog

Platform release histories live with their applications:

- [`macos/CHANGELOG.md`](macos/CHANGELOG.md)
- [`windows/CHANGELOG.md`](windows/CHANGELOG.md)

Current source version on both editions: **1.5.13**.

## 1.5.13 — 2026-10-05

- Large plain-text documents on macOS no longer receive a full-document paragraph update when they open. A file over 256 MB is rejected from its size on disk. `.rtfd` is rejected on Windows. See the platform changelogs.

## 1.5.12 — 2026-10-05

- Line endings, the UTF-8 byte-order mark, Find, and Markdown export stay with the file. See the platform changelogs.

## 1.5.11 — 2026-10-05

- Export as Markdown converts plain text so the note shows the same words and line breaks. A document whose syntax language is already Markdown is written as typed.

## 1.5.10 — 2026-10-05

- Export as Markdown writes a `.md` file from plain text or rich text.

## 1.5.9 — 2026-10-05

- Export as HTML renders plain text that is Markdown, including a note that is not saved as `.md`.

## 1.5.8 — 2026-10-05

- Markdown preview sits beside the editor on both editions. Export as HTML renders a Markdown note. `.md`, `.markdown`, and `.mdown` are recognized.

## 1.5.7 — 2026-10-05

- Plain-text **Export as HTML** writes an existing HTML page through unchanged. The rule is in [`docs/file-behavior.md`](docs/file-behavior.md).
- Help, the feature matrix, and the release notes describe that rule. Windows HTML export adds a UTF-8 BOM. macOS does not.
- About text and help no longer name other editors.

## Repository restructure — 2026-08-16

- Made macOS and Windows self-contained application directories.
- Added independent platform README files and release instructions.
- Added shared behavioral documentation and regression fixtures.
- Renamed `windows-x64/` to `windows/`; architecture remains x64.
- Established independent platform versioning and GitHub tags.
- Added path-scoped GitHub Actions builds for each edition.
