# File behavior contract

The two TextPad editions do not share source code, but they should agree on these observable rules.

## Plain text

- Detect UTF-8 BOM, UTF-16 LE BOM and UTF-16 BE BOM before heuristic detection.
- Detect BOM-less UTF-16 only when byte-position evidence is strong.
- Preserve whether an opened UTF-8 or UTF-16 file used a BOM.
- Reject unrepresentable characters when saving to ASCII or a legacy encoding; never silently replace them with `?`.
- Preserve existing line endings unless the user explicitly selects LF or CRLF conversion.
- Offer an explicit **Open with Encoding** path when automatic detection is wrong.

## Rich text

- Standard `.rtf` is supported.
- `.rtfd` packages are not supported and must produce a clear error.
- Named fonts should survive open, editing and save whenever the native RTF stack exposes them.
- The editor presents RTF using the current application theme. Body text and the page follow the theme; saturated accents and table cell fills stay as in the document when they remain readable.
- Theme presentation must not replace stored RTF colors on save unless the user has edited the document. macOS uses display-only attributes; Windows writes the original bytes for an unedited file.
- Any readability repair that changes an attributed run should be documented because it can affect saved RTF after an edit.

## Word wrap

- When wrap is enabled, lines break at the window edge, including long words with no spaces.
- Wrap is not disabled automatically for extremely long lines.
- Rich text keeps the document's own paragraph wrapping.

## Large documents

- Files up to 256 MB may be opened, subject to available memory.
- Syntax coloring and visual embellishments may be disabled above the large-document threshold.
- Opening should not force complete document layout before the first viewport appears.
- Large documents should not produce autosave snapshots above the snapshot limit.
- Word wrap still follows the user setting on large documents.

## Regression fixtures

Files under `shared/fixtures/` belong to neither platform. When file handling changes, open and round-trip the relevant fixtures on both editions and compare the results.
