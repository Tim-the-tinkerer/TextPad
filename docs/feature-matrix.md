# Feature matrix

This table records intended support, not shared implementation. A checked feature may use completely different platform code.

| Feature | macOS | Windows | Notes |
|---|:---:|:---:|---|
| Plain-text editing | ✓ | ✓ | AppKit text system / AvalonEdit |
| RTF editing | ✓ | ✓ | Platform-native RTF importers differ |
| Syntax highlighting | ✓ | ✓ | Disabled for large documents |
| Tabs and reopen closed tab | ✓ | ✓ | Platform-specific tab interfaces |
| Find, replace and go to line | ✓ | ✓ | |
| Encoding selection | ✓ | ✓ | See `file-behavior.md` |
| BOM preservation | ✓ | ✓ | Existing BOM policy is preserved |
| LF, CRLF and CR detection | ✓ | ✓ | |
| Large-document reductions | ✓ | ✓ | Expensive presentation features are disabled |
| PDF and HTML export | ✓ | ✓ | Different rendering stacks |
| Bundled Interlac fonts | ✓ | ✓ | Registered privately by each app |
| Autosave recovery | ✓ | ✓ | Snapshots are capped for large documents |
| External-change monitoring | ✓ | ✓ | |
| RTFD packages | — | — | Explicitly unsupported |

Update this file when platform behavior diverges intentionally or a feature lands on only one edition.
