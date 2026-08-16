# Shared regression fixtures

These files test behavior shared by the macOS and Windows editions.

- `encoding/` contains plain-text decoding and line-ending samples.
- `rtf/` contains rich-text interoperability samples.
- `large-files/` contains a generator rather than committed large outputs.

Fixtures are test data, not application resources. Neither platform build should package them.
