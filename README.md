# TextPad

TextPad is a lightweight text editor for macOS and Windows, inspired by BBEdit and CotEditor. The two editions share a product identity and a behavioral contract, but they are developed, versioned, built, and released independently.

| Platform | Current version | Implementation | Project |
|---|---:|---|---|
| macOS | 1.5.5 | Swift and AppKit | [`macos/`](macos/) |
| Windows | 1.5.5 | C#, WPF and .NET 8 | [`windows/`](windows/) |

## Repository layout

```text
TextPad/
├── docs/                    Product behavior and release guidance
├── shared/fixtures/         Cross-platform encoding, RTF and large-file samples
├── macos/                   Independent macOS application
│   ├── Sources/
│   ├── Resources/
│   ├── installer/
│   ├── README.md
│   └── CHANGELOG.md
├── windows/                 Independent Windows application
│   ├── TextPad/
│   ├── installer/
│   ├── README.md
│   └── CHANGELOG.md
├── build-macos.sh           Convenience wrapper
├── run-macos.sh             Build and launch the macOS edition
└── build-windows.ps1        Convenience wrapper
```

## Development model

- A platform-specific change only needs to modify and release that platform.
- macOS and Windows versions do not have to remain numerically aligned.
- Shared behavior is documented in [`docs/file-behavior.md`](docs/file-behavior.md).
- Support status is tracked in [`docs/feature-matrix.md`](docs/feature-matrix.md).
- Shared files in `shared/fixtures/` should be tested by both editions when document behavior changes.

Use platform-scoped commit messages such as `macOS: preserve RTF fonts`, `Windows: improve installer signing`, or `Both: add encoding fixtures`.

## Quick build

On macOS:

```bash
bash build-macos.sh
```

On Windows:

```powershell
.\build-windows.ps1
```

See the platform README for requirements, installer commands and output locations.

## Releases

Use independent tags and GitHub releases:

- `macos-v1.5.5`
- `windows-v1.5.5`

Each release should contain only that platform's binaries. See [`docs/releasing.md`](docs/releasing.md).

The path-scoped GitHub Actions workflows likewise build only the affected edition. Changes under `shared/` intentionally trigger both workflows.

If applying this layout to the existing GitHub working tree, see [`docs/repository-migration.md`](docs/repository-migration.md).

## License

Copyright © 2026. All rights reserved.
