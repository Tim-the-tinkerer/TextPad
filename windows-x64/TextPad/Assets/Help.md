# TextPad Help

TextPad is a lightweight **64-bit** text editor for Windows. It handles everyday plain-text and rich-text editing, syntax highlighting, large files, and multi-tab workflows.

---

## Getting started

- **New document** — File → New, or press **Ctrl+N** / **Ctrl+T**.
- **Open a file** — File → Open, drag files onto the window, or pass paths on the command line.
- **Save** — File → Save (**Ctrl+S**). Use **Save As** (**Ctrl+Shift+S**) to choose a new name or format.
- **Multiple tabs** — Each open document has its own tab. Switch with **Ctrl+Tab** / **Ctrl+Shift+Tab**, or **Ctrl+1** through **Ctrl+9** for the first nine tabs.

Only one TextPad window runs at a time. Opening TextPad again (or double-clicking files while it is running) sends those files to the existing window.

---

## File menu

| Command | Shortcut | Description |
|---------|----------|-------------|
| New | Ctrl+N | New untitled tab |
| New Tab | Ctrl+T | Same as New |
| Open | Ctrl+O | Open one or more files |
| Open with Encoding | — | Open a file using a specific character encoding |
| Open Recent | — | Recently opened files (up to 20) |
| Close Tab | Ctrl+W | Close the active tab (prompts if unsaved) |
| Close Window | Ctrl+Shift+W | Close the window (prompts for all dirty tabs) |
| Save | Ctrl+S | Save the active document |
| Save As | Ctrl+Shift+S | Save under a new name or format |
| Revert to Saved | — | Discard changes and reload from disk |
| Document Encoding | — | Change encoding for the current plain-text document |
| Print | Ctrl+P | Print the active document |
| Export as PDF | — | Export plain or rich text to PDF |
| Export as HTML | — | Export to a standalone HTML file |
| Exit | Alt+F4 | Quit TextPad |

### Supported formats

- **Plain text** — Any text file; encoding detected or chosen on open.
- **Rich text (RTF)** — Opened and edited with full formatting. Use Format → Make Plain Text to convert to plain text.

### Character encodings

Plain-text documents support UTF-8, UTF-8 with BOM, UTF-16 LE/BE, ASCII, ISO Latin-1, and Windows Latin-1 (CP-1252). Use **Open with Encoding** when automatic detection is wrong, or **Document Encoding** to change the encoding of an open file (you may be prompted to reload).

### Line endings

The status bar shows **LF**, **CRLF**, **CR**, or **Mixed** for plain-text files. Set the default for new saves in **View → Preferences → Line endings on save** (Preserve, LF, or CRLF).

---

## Edit menu

| Command | Shortcut | Description |
|---------|----------|-------------|
| Undo | Ctrl+Z | Undo last edit |
| Redo | Ctrl+Y | Redo |
| Cut | Ctrl+X | Cut selection |
| Copy | Ctrl+C | Copy selection |
| Paste | Ctrl+V | Paste |
| Paste and Match Style | Ctrl+Shift+V | Paste using surrounding style (rich text) |
| Select All | Ctrl+A | Select entire document |
| Find | Ctrl+F | Show the find bar |
| Find and Replace | Ctrl+H | Open find/replace dialog |
| Find Next | Ctrl+G | Next match |
| Find Previous | Ctrl+Shift+G | Previous match |
| Go to Line | Ctrl+L | Jump to a line number |

### Find bar

When visible, type to search as you type. Use **Aa** for match case, **◀** / **▶** for previous/next match. Press **Enter** for next, **Shift+Enter** for previous, **Esc** to close.

### Find and Replace dialog

Supports **Match case**, **Whole word**, and **Regular expression**. Use **Find Next**, **Replace**, or **Replace All**.

---

## Format menu (rich text)

Available when the document is rich text (RTF):

- **Bold** (Ctrl+B), **Italic** (Ctrl+I), **Underline** (Ctrl+U), Strikethrough
- **Text Color** and **Highlight Color**
- Alignment: Left, Center, Right, Justify
- **Increase / Decrease Indent** (Ctrl+] / Ctrl+[)
- **Make Rich Text** — Convert plain text to rich text
- **Make Plain Text** — Strip formatting

---

## View menu

| Option | Description |
|--------|-------------|
| Zoom In / Out | Ctrl++ / Ctrl+- |
| Word Wrap | Wrap long lines (see Large files below) |
| Line Numbers | Show or hide the gutter |
| Show Invisibles | Display spaces, tabs, and line endings |
| Highlight Current Line | Shade the line containing the caret |
| Syntax Highlighting | Auto-detect or pick a language manually |
| Themes | Light, Dark, Solarized, Sepia, or System |
| Preferences | Editor settings (see below) |

### Syntax highlighting languages

Auto, Plain Text, C#, JavaScript, Python, HTML, CSS, JSON, Markdown, Shell, C/C++, and more. Language is chosen from the file extension when set to **Auto**.

---

## Window menu

| Command | Shortcut | Description |
|---------|----------|-------------|
| New Tab | Ctrl+T | New tab |
| Close Tab | Ctrl+W | Close active tab |
| Reopen Closed Tab | Ctrl+Shift+T | Restore last closed tab |
| Next Tab | Ctrl+Tab | Next tab |
| Previous Tab | Ctrl+Shift+Tab | Previous tab |

---

## Preferences

Open via **View → Preferences**.

| Setting | Description |
|---------|-------------|
| Theme | Light, Dark, Solarized, Sepia, or follow Windows (System) |
| Font | Editor font family (plain text) |
| Font size | 8–72 pt |
| Tab width | Spaces per tab when converting tabs |
| Line endings on save | Preserve, LF, or CRLF |
| Word wrap | Enable wrapping for normal-sized lines |
| Show line numbers | Gutter on/off |
| Highlight current line | Current-line highlight on/off |
| Show invisible characters | Whitespace markers on/off |
| Auto-save | Periodically save recovery snapshots |
| Auto-save interval | 15–600 seconds (default 60) |

Preferences are stored in:

`%APPDATA%\com.textpad.editor\settings.json`

---

## Large files

TextPad uses two editors:

1. **Standard editor** — Full syntax highlighting, find, and editing for typical files.
2. **Large-file editor** — Used when a file is **≥ 500 KB** and contains lines longer than **8,000 characters**. Uses a fast wrapped-text view with line numbers and word wrap forced on for readability.

Very large single-line files (for example multi-megabyte JSON or data exports) open without freezing the UI. Plain-text files load on a background thread; the tab title shows a loading indicator until content is ready.

Syntax highlighting is disabled for documents over **500,000 characters** to keep the UI responsive.

---

## Auto-save and recovery

When auto-save is enabled, TextPad saves recovery snapshots of open documents to:

`%APPDATA%\com.textpad.editor\Autosave\`

If TextPad closes unexpectedly, you are prompted on the next launch to recover unsaved work. You can replace the open tab, keep the current version, or dismiss recovery for that snapshot.

Auto-save is limited to documents under **500,000 characters** to avoid excessive disk use.

---

## External file changes

If a file open in TextPad is modified, deleted, or moved on disk, you are notified. Deleted files are marked **(missing)** in the tab title; use **Save As** to keep your changes.

---

## Export and print

- **Print** — Sends the document to the system print dialog.
- **Export as PDF** — Plain text is rendered to PDF; rich text keeps formatting where possible.
- **Export as HTML** — Rich text exports as HTML; plain text as a simple HTML page.

---

## Status bar

The status bar shows:

- **Line and column** of the caret
- **Character count**
- **Encoding** (or RTF for rich text)
- **Line ending** style (plain text only)

---

## Drag and drop

Drop one or more files onto the TextPad window to open them in new tabs.

---

## Command line

```
TextPad.exe [file1] [file2] ...
```

Paths with spaces should be quoted. Additional launches forward files to the running instance.

---

## Data and log files

| Location | Purpose |
|----------|---------|
| `%APPDATA%\com.textpad.editor\settings.json` | Preferences and recent files |
| `%APPDATA%\com.textpad.editor\Autosave\` | Recovery snapshots |
| `%APPDATA%\com.textpad.editor\crash.log` | Error log for troubleshooting |

---

## Keyboard shortcuts (quick reference)

### File
- Ctrl+N / Ctrl+T — New tab
- Ctrl+O — Open
- Ctrl+S — Save
- Ctrl+Shift+S — Save As
- Ctrl+W — Close tab
- Ctrl+Shift+W — Close window
- Ctrl+P — Print

### Edit
- Ctrl+Z — Undo
- Ctrl+Y — Redo
- Ctrl+X / Ctrl+C / Ctrl+V — Cut / Copy / Paste
- Ctrl+Shift+V — Paste and match style
- Ctrl+A — Select all
- Ctrl+F — Find
- Ctrl+H — Find and replace
- Ctrl+G / Ctrl+Shift+G — Find next / previous
- Ctrl+L — Go to line

### Format (rich text)
- Ctrl+B / Ctrl+I / Ctrl+U — Bold / Italic / Underline

### View
- Ctrl++ / Ctrl+- — Zoom in / out

### Window
- Ctrl+Tab / Ctrl+Shift+Tab — Next / previous tab
- Ctrl+1 … Ctrl+9 — Switch to tab 1–9
- Ctrl+Shift+T — Reopen closed tab

### Help
- F1 — Open this help file

---

## Troubleshooting

**TextPad will not open a second window**  
This is intentional. Files are sent to the already-running instance.

**Garbled or wrong characters**  
Use File → Open with Encoding and pick the correct encoding, or File → Document Encoding on an open file.

**Slow or frozen on a huge file**  
Very large single-line files use the large-file editor automatically. Wait for the loading title to clear.

**SmartScreen warning on the installer**  
The installer may be unsigned or signed with a development certificate. Only install from a source you trust.

**Report a problem**  
Check `%APPDATA%\com.textpad.editor\crash.log` for details after an error.

---

## About

TextPad is inspired by BBEdit and CotEditor. See **Help → About TextPad** for the installed version number.

© TextPad