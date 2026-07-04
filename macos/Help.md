# TextPad Help

TextPad is a lightweight text editor for macOS. It handles everyday plain-text and rich-text editing, syntax highlighting, large files, and multi-tab workflows.

---

## Getting started

- **New document** — File → New, or press **⌘N** / **⌘T**.
- **Open a file** — File → Open, drag files onto the window, or double-click a supported file type.
- **Save** — File → Save (**⌘S**). Use **Save As** (**⌘⇧S**) to choose a new name or format.
- **Multiple tabs** — Each open document has its own tab in the window.

Only one TextPad instance runs at a time. Launching TextPad again (or opening files while it is running) sends those files to the existing instance.

---

## File menu

| Command | Shortcut | Description |
|---------|----------|-------------|
| New | ⌘N | New untitled tab |
| New Tab | ⌘T | Same as New |
| Open | ⌘O | Open one or more files |
| Open with Encoding | — | Open a file using a specific character encoding |
| Open Recent | — | Recently opened files |
| Close Tab | ⌘W | Close the active tab (prompts if unsaved) |
| Close Window | ⌘⇧W | Close the window (prompts for all dirty tabs) |
| Save | ⌘S | Save the active document |
| Save As | ⌘⇧S | Save under a new name or format |
| Revert to Saved | — | Discard changes and reload from disk |
| Document Encoding | — | Change encoding for the current plain-text document |
| Print | ⌘P | Print the active document |
| Export as PDF | — | Export plain or rich text to PDF |
| Export as HTML | — | Export to a standalone HTML file |

### Supported formats

- **Plain text** — Any text file; encoding detected or chosen on open.
- **Rich text (RTF)** — Opened and edited with full formatting. Use Format → Make Plain Text to convert to plain text.

### Character encodings

Plain-text documents support UTF-8, UTF-8 with BOM, UTF-16 LE/BE, ASCII, and ISO Latin-1. Use **Open with Encoding** when automatic detection is wrong, or **Document Encoding** to change the encoding of an open file.

### Line endings

The status bar shows **LF**, **CRLF**, **CR**, or **Mixed** for plain-text files. Set the default for new saves in **TextPad → Preferences → Line endings on save** (Preserve, LF, or CRLF).

---

## Edit menu

| Command | Shortcut | Description |
|---------|----------|-------------|
| Undo | ⌘Z | Undo last edit |
| Redo | ⌘⇧Z | Redo |
| Cut | ⌘X | Cut selection |
| Copy | ⌘C | Copy selection |
| Paste | ⌘V | Paste |
| Paste and Match Style | ⌘⇧V | Paste using surrounding style (rich text) |
| Select All | ⌘A | Select entire document |
| Find | ⌘F | Show the find bar |
| Find and Replace | ⌘⌥F | Open find/replace dialog |
| Find Next | ⌘G | Next match |
| Find Previous | ⌘⇧G | Previous match |
| Go to Line | ⌘L | Jump to a line number |

### Find bar

When visible, type to search. Use **Aa** for match case, **◀** / **▶** for previous/next match. Press **Return** for next, **⇧Return** for previous, **Esc** to close.

### Find and Replace dialog

Supports **Match case**, **Whole word**, and **Regular expression**. Use **Find Next**, **Replace**, or **Replace All**.

---

## Format menu (rich text)

Available when the document is rich text (RTF):

- **Bold** (⌘B), **Italic** (⌘I), **Underline** (⌘U), Strikethrough
- **Show Fonts** (⌘T), **Text Color**, and **Highlight Color**
- Alignment: Left (⌘{), Center (⌘|), Right (⌘})
- **Bullet List** / **Numbered List**
- **Increase / Decrease Indent** (⌘] / ⌘[)
- **Make Rich Text** — Convert plain text to rich text
- **Make Plain Text** — Strip formatting

---

## View menu

| Option | Shortcut | Description |
|--------|----------|-------------|
| Zoom In / Out | ⌘+ / ⌘- | Increase or decrease font size |
| Toggle Line Numbers | ⌘⇧L | Show or hide the gutter |
| Toggle Word Wrap | — | Wrap long lines |
| Toggle Invisibles | ⌘⌥I | Display spaces, tabs, and line endings |
| Toggle Current Line Highlight | — | Shade the line containing the caret |
| Syntax Highlighting | — | Pick a language manually |

Themes and other editor settings are in **TextPad → Preferences** (**⌘,**).

### Syntax highlighting languages

Plain Text, Swift, Python, JavaScript, HTML, CSS, JSON, Markdown, Shell, and C/C++. Language is chosen from the file extension when you open a file.

---

## Window menu

| Command | Shortcut | Description |
|---------|----------|-------------|
| Minimize | ⌘M | Minimize the window |
| Reopen Closed Tab | ⌘⇧T | Restore last closed tab |
| Bring All to Front | — | Bring all TextPad windows forward |

---

## Preferences

Open via **TextPad → Preferences** (**⌘,**).

| Setting | Description |
|---------|-------------|
| Theme | Light, Dark, Solarized, Sepia, or follow macOS (System) |
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

Preferences are stored in macOS **UserDefaults** under the `com.textpad.editor` domain.

---

## Large files

- Files over **100 MB** cannot be opened.
- Very long lines (**> 8,000 characters**) automatically disable word wrap on open for readability.
- Syntax highlighting is disabled for documents over **500,000 characters** to keep the UI responsive.
- The editor grows with content so large files scroll correctly.

---

## Auto-save and recovery

When auto-save is enabled, TextPad saves recovery snapshots of open documents to:

`~/Library/Application Support/com.textpad.editor/Autosave/`

If TextPad closes unexpectedly, you are prompted on the next launch to recover unsaved work.

Auto-save is limited to documents under **500,000 characters** to avoid excessive disk use.

---

## External file changes

If a file open in TextPad is modified on disk by another application, you are prompted to **Reload** or **Keep Current Version**. Keeping your version marks the document as edited so a later Save does not silently overwrite disk.

---

## Export and print

- **Print** — Sends the document to the system print dialog.
- **Export as PDF** — Plain text is rendered to PDF; rich text keeps formatting where possible.
- **Export as HTML** — Rich text exports as HTML; plain text as a simple HTML page.

---

## Status bar

The status bar shows:

- **Line and column** of the caret
- **Language** or Rich Text
- **Encoding** (or RTF for rich text)
- **Line ending** style (plain text only)
- **Character count**

---

## Drag and drop

Drop one or more files onto the TextPad window to open them in new tabs.

---

## Command line

```
TextPad /path/to/file1 /path/to/file2
```

Paths with spaces should be quoted. Additional launches forward files to the running instance.

---

## Data and log files

| Location | Purpose |
|----------|---------|
| `~/Library/Preferences/com.textpad.editor.plist` | Preferences |
| `~/Library/Application Support/com.textpad.editor/Autosave/` | Recovery snapshots |
| `~/Library/Application Support/com.textpad.editor/crash.log` | Error log for troubleshooting |

---

## Keyboard shortcuts (quick reference)

### File
- ⌘N / ⌘T — New tab
- ⌘O — Open
- ⌘S — Save
- ⌘⇧S — Save As
- ⌘W — Close tab
- ⌘⇧W — Close window
- ⌘P — Print

### Edit
- ⌘Z — Undo
- ⌘⇧Z — Redo
- ⌘X / ⌘C / ⌘V — Cut / Copy / Paste
- ⌘⇧V — Paste and match style
- ⌘A — Select all
- ⌘F — Find
- ⌘⌥F — Find and replace
- ⌘G / ⌘⇧G — Find next / previous
- ⌘L — Go to line

### Format (rich text)
- ⌘B / ⌘I / ⌘U — Bold / Italic / Underline

### View
- ⌘+ / ⌘- — Zoom in / out
- ⌘⇧L — Toggle line numbers
- ⌘⌥I — Toggle invisibles

### Window
- ⌘⇧T — Reopen closed tab

### Help
- F1 — Open this help file

### Application
- ⌘, — Preferences
- ⌘Q — Quit TextPad

---

## Troubleshooting

**TextPad will not open a second window**  
This is intentional. Files are sent to the already-running instance.

**Garbled or wrong characters**  
Use File → Open with Encoding and pick the correct encoding, or File → Document Encoding on an open file.

**Selection is hard to see**  
TextPad uses distinct colors for text selection and the current-line highlight. You can turn off the current-line highlight in View → Toggle Current Line Highlight or in Preferences.

**Gatekeeper warning**  
If the app is unsigned or ad-hoc signed, macOS may warn on first open. Open via **System Settings → Privacy & Security** or right-click → Open.

**Report a problem**  
Check `~/Library/Application Support/com.textpad.editor/crash.log` for details after an error.

---

## About

TextPad is inspired by BBEdit and CotEditor. See **TextPad → About TextPad** for the installed version number.

© TextPad