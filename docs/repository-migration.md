# Repository migration notes

This restructure preserves the existing repository history. Git may initially display many deletions and additions; after the change is staged, it should recognize most source moves as renames.

| Previous path | New path |
|---|---|
| `macos/*.swift` | `macos/Sources/*.swift` |
| `macos/Fonts/` | `macos/Resources/Fonts/` |
| `macos/Info.plist` | `macos/Resources/Info.plist` |
| `macos/Help.md` | `macos/Resources/Help.md` |
| `windows-x64/` | `windows/` |
| platform test samples | `shared/fixtures/` |

For the first commit after replacing the working tree:

1. Review the changes in GitHub Desktop.
2. Confirm that generated directories such as `.build`, `bin`, `obj`, `dist` and `tools` are not staged.
3. Commit with a summary such as `Restructure macOS and Windows projects`.
4. Push normally to the existing repository.

No new GitHub repository is required. Existing history, issues and releases can remain where they are.
