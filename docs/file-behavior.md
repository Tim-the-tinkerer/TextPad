# File behavior contract

The two TextPad editions do not share source code, but they should agree on these observable rules.

## Plain text

- Detect UTF-8 BOM, UTF-16 LE BOM and UTF-16 BE BOM before heuristic detection.
- Detect BOM-less UTF-16 only when byte-position evidence is strong.
- Preserve whether an opened UTF-8 or UTF-16 file used a BOM.
- Reject unrepresentable characters when saving to ASCII or a legacy encoding; never silently replace them with `?`.
- Preserve existing line endings unless the user explicitly selects LF or CRLF conversion.
- Pressing Return inserts the document's current line ending. LF, CRLF, and CR each stay themselves. A mixed document inserts LF.
- Offer an explicit **Open with Encoding** path when automatic detection is wrong.

## Rich text

- Standard `.rtf` is supported.
- `.rtfd` packages are not supported and must produce a clear error.
- Named fonts should survive open, editing and save whenever the native RTF stack exposes them.
- The editor presents RTF using the current application theme. Body text and the page follow the theme; saturated accents and table cell fills stay as in the document when they remain readable.
- Theme presentation must not replace stored RTF colors on save unless the user has edited the document. macOS uses display-only attributes; Windows writes the original bytes for an unedited file.
- Any readability repair that changes an attributed run should be documented because it can affect saved RTF after an edit.

## HTML export

- Plain text that is not an HTML document exports as a standalone page with the text inside `<pre>`, with `<`, `>`, `&`, and `"` escaped.
- Plain text that is already an HTML document exports unchanged. The document may begin with a BOM, whitespace, HTML comments, or an XML declaration, and then `<!DOCTYPE html` or `<html`. A browser then runs that page instead of showing the source.
- A later mention of those tags, or a fragment that starts with another element, still exports as escaped text.
- Rich text still exports as formatted HTML on a white page. Line endings inside a passed-through HTML document are left as the editor holds them.
- macOS writes the export as UTF-8 and does not add a BOM. When an HTML page is written through unchanged, a UTF-8 BOM already recorded for that document is kept. Windows writes every HTML export as UTF-8 with a BOM.
- When the syntax language is Markdown and the document is plain text, Export as HTML renders the note. Headings, lists, links, tables, fenced code, strikethrough, and task lists are included. Markup typed in the note is escaped and shown as text. Links may use http, https, mailto, a fragment, or a relative address. A document that is already an HTML page is still written through unchanged, before any Markdown rendering. The rendered page uses a light background. Windows still adds a UTF-8 BOM around that page.

## Markdown export

- File → Export as Markdown writes a `.md` file and leaves the open document unchanged.
- Plain text is converted. The Markdown shows the same words and line breaks. Characters that Markdown would treat as formatting, headings, or lists are escaped, and tabs are written as spaces. The file keeps the document's encoding, byte-order mark, and line-ending policy. A document whose syntax language is Markdown is written as typed.
- Rich text is converted. Bold, italic, and bold italic become `**`, `*`, and `***`. Strikethrough becomes `~~`. Underline becomes `<u>…</u>`. Links become `[label](url)`. Bullet lists become `-` items and numbered lists become `1.` items. Items in the same list stay on consecutive lines, including a nested item under its parent. A bullet list followed by a numbered list is separated by a blank line. Tables become pipe tables. Paragraphs are separated by a blank line.
- Font, color, and alignment are not written. A rich-text Markdown file is UTF-8 without a BOM, with LF line endings.

## Markdown

- `.md`, `.markdown`, and `.mdown` open as Markdown.
- View → Markdown Preview (⌘⇧M on macOS, Ctrl+Shift+M on Windows) shows the rendered note beside the editor. The preview is off until you turn it on, and each tab remembers its own setting. It follows the editor theme.
- Preview is unavailable above the 500,000-character large-document threshold. The pane then shows a short notice.

## Word wrap

- When wrap is enabled, lines break at the window edge, including long words with no spaces.
- Wrap is not disabled automatically for extremely long lines.
- Rich text keeps the document's own paragraph wrapping.

## Large documents

- Files up to 256 MB may be opened, subject to available memory. The limit is read from the file size on disk before the file is read.
- Syntax coloring, Markdown preview, and visual embellishments may be disabled above the large-document threshold.
- Opening should not force complete document layout before the first viewport appears.
- Opening a large plain-text document, or applying preferences to it, must not rewrite paragraph style across the whole text. Word wrap still follows the user setting.
- The displayed line ending is recalculated after an edit when the document is within the large-document threshold. A larger document keeps the ending detected when it was opened. Return uses that ending.
- Large documents should not produce autosave snapshots above the snapshot limit.
- Word wrap still follows the user setting on large documents.

## Regression fixtures

Files under `shared/fixtures/` belong to neither platform. When file handling changes, open and round-trip the relevant fixtures on both editions and compare the results.
