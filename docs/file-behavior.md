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
- Application themes must not broadly recolor the stored document.
- Any readability repair that changes an attributed run should be documented because it can affect saved RTF.

## Large documents

- Files up to 256 MB may be opened, subject to available memory.
- Syntax coloring and visual embellishments may be disabled above the large-document threshold.
- Opening should not force complete document layout before the first viewport appears.
- Large documents should not produce autosave snapshots above the snapshot limit.

## Regression fixtures

Files under `shared/fixtures/` belong to neither platform. When file handling changes, open and round-trip the relevant fixtures on both editions and compare the results.
