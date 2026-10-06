# Shared regression fixtures

These files test behavior shared by the macOS and Windows editions.

- `encoding/` contains plain-text decoding and line-ending samples.
- `rtf/` contains rich-text interoperability samples.
- `large-files/` contains a generator rather than committed large outputs.

HTML export has no fixture file. The pass-through rule for an existing HTML page is in [`docs/file-behavior.md`](../../docs/file-behavior.md).

Fixtures are test data, not application resources. Neither platform build should package them.
