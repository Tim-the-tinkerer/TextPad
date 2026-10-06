# Feature matrix

This table records intended support, not shared implementation. A checked feature may use completely different platform code.

| Feature | macOS | Windows | Notes |
|---|:---:|:---:|---|
| Plain-text editing | ✓ | ✓ | AppKit text system / AvalonEdit |
| RTF editing | ✓ | ✓ | Platform-native RTF importers differ; viewing follows the application theme |
| Word wrap | ✓ | ✓ | Forced wrap at the window edge when enabled; shortcut ⌘\\ / Ctrl+\\ |
| Syntax highlighting | ✓ | ✓ | Disabled for large documents |
| Tabs and reopen closed tab | ✓ | ✓ | Platform-specific tab interfaces |
| Find, replace and go to line | ✓ | ✓ | |
| Encoding selection | ✓ | ✓ | See `file-behavior.md` |
| BOM preservation | ✓ | ✓ | Existing BOM policy is preserved |
| LF, CRLF and CR detection | ✓ | ✓ | |
| Large-document reductions | ✓ | ✓ | Expensive presentation features are disabled |
| PDF and HTML export | ✓ | ✓ | Different rendering stacks. A plain-text HTML page is written through unchanged. A Markdown note is rendered when that is the syntax language. Windows HTML export adds a UTF-8 BOM; macOS does not. |
| Export as Markdown | ✓ | ✓ | Plain text becomes Markdown that shows the same words and line breaks. A Markdown note is written as typed. Rich text becomes emphasis, strike, underline, lists, links, and tables. |
| Markdown preview | ✓ | ✓ | Per tab, off until opened, follows the editor theme. Unavailable above 500,000 characters. |
| Bundled Interlac fonts | ✓ | ✓ | Registered privately by each app |
| Autosave recovery | ✓ | ✓ | Snapshots are capped for large documents |
| External-change monitoring | ✓ | ✓ | |
| RTFD packages | — | — | Explicitly unsupported |

Update this file when platform behavior diverges intentionally or a feature lands on only one edition.
