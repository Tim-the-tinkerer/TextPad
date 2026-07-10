import AppKit
import UniformTypeIdentifiers

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var windows: [DocumentWindowController] = []
    private let singleInstanceManager: SingleInstanceManager
    private var findController = FindReplaceController()

    init(singleInstanceManager: SingleInstanceManager) {
        self.singleInstanceManager = singleInstanceManager
        super.init()
    }
    private var goToLineController = GoToLineController()
    private var preferencesController: PreferencesWindowController?
    private var textColorController = TextColorController()
    private var highlightColorController = HighlightColorController()
    private var recentDocumentsMenu: NSMenu?
    private var openedFilesAtLaunch = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        CrashLogger.install()
        EditorPreferences.shared.validate()
        setupMenus()
        singleInstanceManager.startListening { [weak self] command in
            self?.handleSingleInstanceCommand(command)
        }

        // Only create a blank document if no file was opened at launch.
        // openFiles: fires BEFORE this method when launching with a file argument.
        if !openedFilesAtLaunch && windows.isEmpty {
            newDocument(nil)
        }

        offerAutoSaveRecovery()
        observeSystemAppearanceChanges()
        NotificationCenter.default.addObserver(self, selector: #selector(windowDidClose(_:)), name: NSWindow.willCloseNotification, object: nil)
    }

    private func observeSystemAppearanceChanges() {
        DistributedNotificationCenter.default().addObserver(
            self,
            selector: #selector(systemAppearanceChanged),
            name: Notification.Name("AppleInterfaceThemeChangedNotification"),
            object: nil
        )
    }

    @objc private func systemAppearanceChanged() {
        guard EditorPreferences.shared.theme == .system else { return }
        for window in windows {
            window.currentEditor?.applyPreferences()
            window.refreshAppearance()
        }
    }

    func applicationShouldOpenUntitledFile(_ sender: NSApplication) -> Bool {
        false
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    func application(_ sender: NSApplication, openFiles filenames: [String]) {
        openedFilesAtLaunch = true
        for path in filenames {
            openFile(at: URL(fileURLWithPath: path))
        }
    }

    private func setupMenus() {
        let mainMenu = NSMenu()

        // App menu
        let appMenu = NSMenu()
        let appItem = NSMenuItem()
        appItem.submenu = appMenu
        appMenu.addItem(withTitle: "About TextPad", action: #selector(showAbout), keyEquivalent: "")
        appMenu.addItem(NSMenuItem.separator())
        appMenu.addItem(withTitle: "Preferences…", action: #selector(showPreferences), keyEquivalent: ",")
        appMenu.addItem(NSMenuItem.separator())
        appMenu.addItem(withTitle: "Hide TextPad", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Hide Others", action: #selector(NSApplication.hideOtherApplications(_:)), keyEquivalent: "h").keyEquivalentModifierMask = [.command, .option]
        appMenu.addItem(withTitle: "Quit TextPad", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        mainMenu.addItem(appItem)

        // File menu
        let fileMenu = NSMenu(title: "File")
        fileMenu.addItem(withTitle: "New", action: #selector(newDocument), keyEquivalent: "n")
        fileMenu.addItem(withTitle: "New Tab", action: #selector(newTab), keyEquivalent: "t")
        fileMenu.addItem(withTitle: "Open…", action: #selector(openDocument), keyEquivalent: "o")
        fileMenu.addItem(withTitle: "Open with Encoding…", action: #selector(openWithEncoding), keyEquivalent: "")
        let recentMenu = NSMenu()
        recentMenu.delegate = self
        recentDocumentsMenu = recentMenu
        let recentItem = NSMenuItem(title: "Open Recent", action: nil, keyEquivalent: "")
        recentItem.submenu = recentMenu
        fileMenu.addItem(recentItem)
        fileMenu.addItem(NSMenuItem.separator())
        fileMenu.addItem(withTitle: "Close Tab", action: #selector(closeTab), keyEquivalent: "w")
        fileMenu.addItem(withTitle: "Close Window", action: #selector(closeWindow), keyEquivalent: "w").keyEquivalentModifierMask = [.command, .shift]
        fileMenu.addItem(withTitle: "Save", action: #selector(saveDocumentAction), keyEquivalent: "s")
        fileMenu.addItem(withTitle: "Save As…", action: #selector(saveDocumentAsAction), keyEquivalent: "S")
        fileMenu.addItem(withTitle: "Revert to Saved", action: #selector(revertDocument), keyEquivalent: "")
        fileMenu.addItem(withTitle: "Document Encoding…", action: #selector(showDocumentEncoding), keyEquivalent: "")
        fileMenu.addItem(NSMenuItem.separator())
        fileMenu.addItem(withTitle: "Page Setup…", action: #selector(runPageLayout), keyEquivalent: "")
        fileMenu.addItem(withTitle: "Print…", action: #selector(printDocument), keyEquivalent: "p")
        fileMenu.addItem(NSMenuItem.separator())
        fileMenu.addItem(withTitle: "Export as PDF…", action: #selector(exportAsPDF), keyEquivalent: "")
        fileMenu.addItem(withTitle: "Export as HTML…", action: #selector(exportAsHTML), keyEquivalent: "")
        let fileItem = NSMenuItem()
        fileItem.submenu = fileMenu
        mainMenu.addItem(fileItem)

        // Edit menu
        let editMenu = NSMenu(title: "Edit")
        editMenu.addItem(withTitle: "Undo", action: Selector(("undo:")), keyEquivalent: "z")
        editMenu.addItem(withTitle: "Redo", action: Selector(("redo:")), keyEquivalent: "Z")
        editMenu.addItem(NSMenuItem.separator())
        editMenu.addItem(withTitle: "Cut", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "Paste and Match Style", action: #selector(pasteAndMatchStyle), keyEquivalent: "V")
        editMenu.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        editMenu.addItem(NSMenuItem.separator())
        editMenu.addItem(withTitle: "Find…", action: #selector(showFind), keyEquivalent: "f")
        editMenu.addItem(withTitle: "Find and Replace…", action: #selector(showFindReplace), keyEquivalent: "f").keyEquivalentModifierMask = [.command, .option]
        editMenu.addItem(withTitle: "Find Next", action: #selector(findNext), keyEquivalent: "g")
        editMenu.addItem(withTitle: "Find Previous", action: #selector(findPrevious), keyEquivalent: "G")
        editMenu.addItem(withTitle: "Go to Line…", action: #selector(showGoToLine), keyEquivalent: "l")
        let editItem = NSMenuItem()
        editItem.submenu = editMenu
        mainMenu.addItem(editItem)

        // Format menu
        let formatMenu = NSMenu(title: "Format")
        formatMenu.addItem(withTitle: "Bold", action: #selector(toggleBoldface(_:)), keyEquivalent: "b")
        formatMenu.addItem(withTitle: "Italic", action: #selector(toggleItalics(_:)), keyEquivalent: "i")
        formatMenu.addItem(withTitle: "Underline", action: #selector(underline(_:)), keyEquivalent: "u")
        formatMenu.addItem(withTitle: "Strikethrough", action: #selector(strikethrough), keyEquivalent: "")
        formatMenu.addItem(NSMenuItem.separator())
        formatMenu.addItem(withTitle: "Show Fonts", action: #selector(showFonts(_:)), keyEquivalent: "t")
        formatMenu.addItem(withTitle: "Text Color…", action: #selector(showTextColor), keyEquivalent: "C").keyEquivalentModifierMask = [.command, .shift]
        formatMenu.addItem(withTitle: "Highlight Color…", action: #selector(showHighlightColor), keyEquivalent: "h").keyEquivalentModifierMask = [.command, .option]
        formatMenu.addItem(NSMenuItem.separator())
        formatMenu.addItem(withTitle: "Align Left", action: #selector(alignLeft), keyEquivalent: "{")
        formatMenu.addItem(withTitle: "Align Center", action: #selector(alignCenter), keyEquivalent: "|")
        formatMenu.addItem(withTitle: "Align Right", action: #selector(alignRight), keyEquivalent: "}")
        formatMenu.addItem(withTitle: "Justify", action: #selector(alignJustified), keyEquivalent: "")
        formatMenu.addItem(NSMenuItem.separator())
        formatMenu.addItem(withTitle: "Bullet List", action: #selector(toggleBulletList), keyEquivalent: "8").keyEquivalentModifierMask = [.command, .shift]
        formatMenu.addItem(withTitle: "Numbered List", action: #selector(toggleNumberedList), keyEquivalent: "7").keyEquivalentModifierMask = [.command, .shift]
        formatMenu.addItem(withTitle: "Increase Indent", action: #selector(increaseIndent), keyEquivalent: "]")
        formatMenu.addItem(withTitle: "Decrease Indent", action: #selector(decreaseIndent), keyEquivalent: "[")
        formatMenu.addItem(NSMenuItem.separator())
        formatMenu.addItem(withTitle: "Make Rich Text", action: #selector(makeRichText), keyEquivalent: "")
        formatMenu.addItem(withTitle: "Make Plain Text", action: #selector(makePlainText), keyEquivalent: "")
        let formatItem = NSMenuItem()
        formatItem.submenu = formatMenu
        mainMenu.addItem(formatItem)

        // View menu
        let viewMenu = NSMenu(title: "View")
        viewMenu.addItem(withTitle: "Zoom In", action: #selector(zoomIn), keyEquivalent: "+")
        viewMenu.addItem(withTitle: "Zoom Out", action: #selector(zoomOut), keyEquivalent: "-")
        viewMenu.addItem(NSMenuItem.separator())
        viewMenu.addItem(withTitle: "Toggle Line Numbers", action: #selector(toggleLineNumbers), keyEquivalent: "l").keyEquivalentModifierMask = [.command, .shift]
        viewMenu.addItem(withTitle: "Toggle Word Wrap", action: #selector(toggleWordWrap), keyEquivalent: "")
        viewMenu.addItem(withTitle: "Toggle Invisibles", action: #selector(toggleInvisibles), keyEquivalent: "i").keyEquivalentModifierMask = [.command, .option]
        viewMenu.addItem(withTitle: "Toggle Current Line Highlight", action: #selector(toggleCurrentLineHighlight), keyEquivalent: "")
        viewMenu.addItem(NSMenuItem.separator())
        let langMenu = NSMenu()
        for lang in SyntaxLanguage.allCases {
            langMenu.addItem(withTitle: lang.displayName, action: #selector(setLanguage(_:)), keyEquivalent: "")
        }
        let langItem = NSMenuItem(title: "Syntax Highlighting", action: nil, keyEquivalent: "")
        langItem.submenu = langMenu
        viewMenu.addItem(langItem)
        let viewItem = NSMenuItem()
        viewItem.submenu = viewMenu
        mainMenu.addItem(viewItem)

        // Window menu
        let windowMenu = NSMenu(title: "Window")
        windowMenu.addItem(withTitle: "Minimize", action: #selector(NSWindow.miniaturize(_:)), keyEquivalent: "m")
        windowMenu.addItem(withTitle: "Zoom", action: #selector(NSWindow.zoom(_:)), keyEquivalent: "")
        windowMenu.addItem(withTitle: "Reopen Closed Tab", action: #selector(reopenClosedTab), keyEquivalent: "t").keyEquivalentModifierMask = [.command, .shift]
        windowMenu.addItem(NSMenuItem.separator())
        windowMenu.addItem(withTitle: "Bring All to Front", action: #selector(bringAllToFront), keyEquivalent: "")
        let windowItem = NSMenuItem()
        windowItem.submenu = windowMenu
        mainMenu.addItem(windowItem)
        NSApp.windowsMenu = windowMenu

        // Help menu
        let helpMenu = NSMenu(title: "Help")
        let helpItem = NSMenuItem(
            title: "TextPad Help",
            action: #selector(openHelp),
            keyEquivalent: "\u{F704}"
        )
        helpMenu.addItem(helpItem)
        let helpMenuItem = NSMenuItem()
        helpMenuItem.submenu = helpMenu
        mainMenu.addItem(helpMenuItem)
        NSApp.helpMenu = helpMenu

        NSApp.mainMenu = mainMenu
        NSApp.servicesMenu = NSMenu()

        setTarget(self, for: fileMenu)
        setTarget(self, for: editMenu, excluding: Self.responderChainEditActions)
        setTarget(self, for: formatMenu)
        setTarget(self, for: viewMenu)
        setTarget(self, for: windowMenu, excluding: Self.responderChainWindowActions)
        setTarget(self, for: helpMenu)
    }

    private static let responderChainEditActions: Set<Selector> = [
        Selector(("undo:")),
        Selector(("redo:")),
        #selector(NSText.cut(_:)),
        #selector(NSText.copy(_:)),
        #selector(NSText.paste(_:)),
        #selector(NSText.selectAll(_:))
    ]

    private static let responderChainWindowActions: Set<Selector> = [
        #selector(NSWindow.miniaturize(_:)),
        #selector(NSWindow.zoom(_:))
    ]

    private func setTarget(_ target: AnyObject, for menu: NSMenu, excluding: Set<Selector> = []) {
        for item in menu.items {
            if let submenu = item.submenu {
                setTarget(target, for: submenu, excluding: excluding)
            } else if let action = item.action, !excluding.contains(action) {
                item.target = target
            }
        }
    }

    private func activeWindow() -> DocumentWindowController? {
        (NSApp.keyWindow?.windowController as? DocumentWindowController) ?? windows.last
    }

    @objc private func windowDidClose(_ notification: Notification) {
        guard let window = notification.object as? NSWindow,
              let wc = window.windowController as? DocumentWindowController else { return }
        windows.removeAll { $0 === wc }
    }

    // MARK: - File Actions

    @objc func newDocument(_ sender: Any?) {
        let wc = DocumentWindowController()
        windows.append(wc)
        wc.newUntitledDocument()
        wc.showWindow(nil)
        wc.window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc private func newTab(_ sender: Any?) {
        activeWindow()?.newUntitledDocument()
    }

    @objc private func openDocument(_ sender: Any?) {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.allowsOtherFileTypes = true
        panel.allowedContentTypes = [.plainText, .rtf, .sourceCode, .json, .html, .xml, .script, .data]
        panel.begin { response in
            guard response == .OK else { return }
            for url in panel.urls {
                self.openFile(at: url)
            }
        }
    }

    func openFile(at url: URL) {
        let doc = EditorDocument()
        do {
            try doc.load(from: url)
        } catch {
            showError("Could not open file", error.localizedDescription)
            return
        }
        presentDocument(doc)
    }

    func presentDocument(_ doc: EditorDocument) {
        let wc: DocumentWindowController
        if let active = activeWindow(), active.hasSingleEmptyUntitledTab() {
            wc = active
            wc.replaceWithDocument(doc)
        } else if let active = activeWindow() {
            wc = active
            wc.openDocument(doc)
        } else {
            wc = DocumentWindowController()
            windows.append(wc)
            wc.showWindow(nil)
            wc.openDocument(doc)
        }

        wc.window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc private func openWithEncoding(_ sender: Any?) {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.allowsOtherFileTypes = true
        panel.allowedContentTypes = [.plainText, .sourceCode, .json, .html, .xml, .script, .data]
        panel.begin { response in
            guard response == .OK, let url = panel.url else { return }
            let controller = EncodingOptionsController(mode: .open)
            controller.show { encoding, policy in
                let doc = EditorDocument()
                do {
                    try doc.load(from: url, encoding: encoding)
                    doc.lineEndingPolicy = policy
                    self.presentDocument(doc)
                } catch {
                    self.showError("Could not open file", error.localizedDescription)
                }
            }
        }
    }

    @objc private func showDocumentEncoding(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor, !editor.document.isRichText else { return }
        let controller = EncodingOptionsController(mode: .document(editor.document))
        controller.show { encoding, policy in
            editor.document.applyEncodingSettings(encoding: encoding, lineEndingPolicy: policy)
            editor.updateStatusBar()
        }
    }

    private func offerAutoSaveRecovery() {
        let entries = AutoSaveManager.pendingRecoveries()
        guard !entries.isEmpty else { return }

        for entry in entries {
            let alert = NSAlert()
            alert.messageText = "Recover auto-saved document?"
            let formatter = RelativeDateTimeFormatter()
            let when = formatter.localizedString(for: entry.savedAt, relativeTo: Date())
            alert.informativeText = "Found an auto-saved copy of \"\(entry.displayName)\" from \(when)."
            alert.addButton(withTitle: "Recover")
            alert.addButton(withTitle: "Discard")
            let response = alert.runModal()
            if response == .alertFirstButtonReturn {
                presentDocument(EditorDocument.fromAutoSave(entry))
            }
            AutoSaveManager.clear(documentID: entry.documentID)
        }
    }

    @objc private func closeTab(_ sender: Any?) {
        activeWindow()?.closeCurrentTab()
    }

    @objc private func closeWindow(_ sender: Any?) {
        activeWindow()?.closeWindowWithSaveChecks()
    }

    @objc private func saveDocumentAction(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        _ = saveEditor(editor: editor)
    }

    @discardableResult
    func saveEditor(editor: EditorViewController) -> Bool {
        editor.syncDocument()
        if editor.document.fileURL == nil {
            return saveEditorAs(editor: editor)
        }
        if !editor.document.fileExtensionMatchesFormat {
            return saveEditorAs(editor: editor)
        }
        do {
            try editor.document.save()
            editor.documentDidSave()
            editor.delegate?.editorDidChange(editor)
            activeWindow()?.updateWindowTitle()
            return true
        } catch {
            showError("Could not save file", error.localizedDescription)
            return false
        }
    }

    @objc private func saveDocumentAsAction(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        _ = saveEditorAs(editor: editor)
    }

    private func saveEditorAs(editor: EditorViewController) -> Bool {
        editor.syncDocument()
        let panel = NSSavePanel()
        if editor.document.isRichText {
            panel.allowedContentTypes = [.rtf]
        } else {
            panel.allowedContentTypes = [.plainText]
        }
        panel.nameFieldStringValue = editor.document.suggestedSaveFileName
        let response = panel.runModal()
        guard response == .OK, let url = panel.url else { return false }
        do {
            try editor.document.save(to: url)
            editor.documentDidSave()
            editor.delegate?.editorDidChange(editor)
            activeWindow()?.updateWindowTitle()
            activeWindow()?.updateTabLabels()
            return true
        } catch {
            showError("Could not save file", error.localizedDescription)
            return false
        }
    }

    @objc private func revertDocument(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor, editor.document.fileURL != nil else { return }
        let alert = NSAlert()
        alert.messageText = "Revert to saved version?"
        alert.informativeText = "All unsaved changes will be lost."
        alert.addButton(withTitle: "Revert")
        alert.addButton(withTitle: "Cancel")
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        do {
            try editor.document.revert()
            editor.reloadFromDocument()
            editor.delegate?.editorDidChange(editor)
            activeWindow()?.updateWindowTitle()
        } catch {
            showError("Could not revert", error.localizedDescription)
        }
    }

    @objc private func runPageLayout(_ sender: Any?) {
        NSApp.runPageLayout(sender)
    }

    @objc private func printDocument(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        let printOp = NSPrintOperation(view: editor.activeTextView, printInfo: NSPrintInfo.shared)
        printOp.run()
    }

    // MARK: - Edit Actions

    @objc private func showFind(_ sender: Any?) {
        activeWindow()?.currentEditor?.showFindBar()
    }

    @objc private func showFindReplace(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        findController.showPanel(for: editor.activeTextView)
    }

    @objc private func findNext(_ sender: Any?) {
        activeWindow()?.currentEditor?.findNext()
    }

    @objc private func findPrevious(_ sender: Any?) {
        activeWindow()?.currentEditor?.findPrevious()
    }

    @objc private func reopenClosedTab(_ sender: Any?) {
        guard let document = ClosedTabManager.shared.pop() else {
            NSSound.beep()
            return
        }
        if let active = activeWindow() {
            active.openDocument(document)
        } else {
            presentDocument(document)
        }
    }

    @objc private func showGoToLine(_ sender: Any?) {
        guard let tv = activeWindow()?.currentEditor?.activeTextView else { return }
        goToLineController.showPanel(for: tv)
    }

    // MARK: - View Actions

    @objc private func zoomIn(_ sender: Any?) {
        activeWindow()?.currentEditor?.zoomIn()
    }

    @objc private func zoomOut(_ sender: Any?) {
        activeWindow()?.currentEditor?.zoomOut()
    }

    @objc private func toggleLineNumbers(_ sender: Any?) {
        EditorPreferences.shared.showLineNumbers.toggle()
    }

    @objc private func toggleWordWrap(_ sender: Any?) {
        EditorPreferences.shared.wordWrap.toggle()
    }

    @objc private func toggleInvisibles(_ sender: Any?) {
        EditorPreferences.shared.showInvisibles.toggle()
    }

    @objc private func toggleCurrentLineHighlight(_ sender: Any?) {
        EditorPreferences.shared.showCurrentLineHighlight.toggle()
    }

    @objc private func openRecentDocument(_ sender: NSMenuItem) {
        guard let url = sender.representedObject as? URL else { return }
        guard FileManager.default.fileExists(atPath: url.path) else {
            showError("Could not open file", "The file could not be found.")
            return
        }
        openFile(at: url)
    }

    @objc private func clearRecentDocuments(_ sender: Any?) {
        NSDocumentController.shared.clearRecentDocuments(sender)
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        guard menu === recentDocumentsMenu else { return }
        menu.removeAllItems()

        let urls = NSDocumentController.shared.recentDocumentURLs
        if urls.isEmpty {
            let item = NSMenuItem(title: "No Recent Documents", action: nil, keyEquivalent: "")
            item.isEnabled = false
            menu.addItem(item)
            return
        }

        for url in urls.prefix(12) {
            let item = NSMenuItem(title: url.lastPathComponent, action: #selector(openRecentDocument(_:)), keyEquivalent: "")
            item.representedObject = url
            item.toolTip = url.path
            menu.addItem(item)
        }

        menu.addItem(NSMenuItem.separator())
        let clearItem = NSMenuItem(title: "Clear Menu", action: #selector(clearRecentDocuments(_:)), keyEquivalent: "")
        menu.addItem(clearItem)
    }

    @objc private func setLanguage(_ sender: NSMenuItem) {
        guard let editor = activeWindow()?.currentEditor else { return }
        let lang = SyntaxLanguage.allCases.first { $0.displayName == sender.title } ?? .plain
        editor.setLanguage(lang)
    }

    // MARK: - Format Actions

    private func editorForFormatting() -> EditorViewController? {
        guard let editor = activeWindow()?.currentEditor else { return nil }
        if !editor.document.isRichText {
            editor.setRichTextMode(true)
        }
        return editor
    }

    @objc func toggleBoldface(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        NSApp.sendAction(NSSelectorFromString("toggleBoldface:"), to: tv, from: sender)
    }

    @objc func toggleItalics(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        NSApp.sendAction(NSSelectorFromString("toggleItalics:"), to: tv, from: sender)
    }

    @objc func underline(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        NSApp.sendAction(NSSelectorFromString("underline:"), to: tv, from: sender)
    }

    @objc func showFonts(_ sender: Any?) {
        guard let editor = editorForFormatting() else { return }
        NSFontManager.shared.target = editor.activeTextView
        NSFontManager.shared.orderFrontFontPanel(sender)
    }

    @objc private func showTextColor(_ sender: Any?) {
        guard let editor = editorForFormatting() else { return }
        RichTextFormatting.showTextColorPanel(for: editor.activeTextView, controller: textColorController)
    }

    @objc private func showHighlightColor(_ sender: Any?) {
        guard let editor = editorForFormatting() else { return }
        highlightColorController.show(for: editor.activeTextView)
    }

    @objc private func strikethrough(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.toggleStrikethrough(in: tv)
    }

    @objc private func alignLeft(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.setAlignment(.left, in: tv)
    }

    @objc private func alignCenter(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.setAlignment(.center, in: tv)
    }

    @objc private func alignRight(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.setAlignment(.right, in: tv)
    }

    @objc private func alignJustified(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.setAlignment(.justified, in: tv)
    }

    @objc private func pasteAndMatchStyle(_ sender: Any?) {
        guard let editor = editorForFormatting() else { return }
        RichTextFormatting.pasteAndMatchStyle(in: editor.activeTextView)
    }

    @objc private func toggleBulletList(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.toggleList(.bullet, in: tv)
    }

    @objc private func toggleNumberedList(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.toggleList(.numbered, in: tv)
    }

    @objc private func increaseIndent(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.adjustIndent(in: tv, deltaLevels: 1)
    }

    @objc private func decreaseIndent(_ sender: Any?) {
        guard let tv = editorForFormatting()?.activeTextView else { return }
        RichTextFormatting.adjustIndent(in: tv, deltaLevels: -1)
    }

    @objc private func exportAsPDF(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        editor.syncDocument()

        let panel = NSSavePanel()
        panel.allowedContentTypes = [.pdf]
        panel.nameFieldStringValue = defaultExportName(for: editor, extension: "pdf")
        guard panel.runModal() == .OK, let url = panel.url else { return }

        let data: Data?
        if editor.document.isRichText {
            data = RichTextFormatting.pdfData(from: editor.activeTextView)
        } else {
            let prefs = EditorPreferences.shared
            data = DocumentExport.pdfData(fromPlainText: editor.document.content, fontSize: prefs.fontSize)
        }

        guard let data else {
            showError("Could not export", "Unable to generate PDF data.")
            return
        }
        do {
            try data.write(to: url, options: .atomic)
        } catch {
            showError("Could not export", error.localizedDescription)
        }
    }

    @objc private func exportAsHTML(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        editor.syncDocument()

        let panel = NSSavePanel()
        panel.allowedContentTypes = [.html]
        panel.nameFieldStringValue = defaultExportName(for: editor, extension: "html")
        guard panel.runModal() == .OK, let url = panel.url else { return }

        let title = editor.document.fileURL?.deletingPathExtension().lastPathComponent ?? "Document"
        do {
            let data: Data
            if editor.document.isRichText {
                data = try RichTextFormatting.htmlData(from: editor.activeTextView)
            } else if let plainData = DocumentExport.htmlData(fromPlainText: editor.document.content, title: title) {
                data = plainData
            } else {
                throw NSError(domain: "TextPad", code: 5, userInfo: [
                    NSLocalizedDescriptionKey: "Unable to generate HTML."
                ])
            }
            try data.write(to: url, options: .atomic)
        } catch {
            showError("Could not export", error.localizedDescription)
        }
    }

    private func defaultExportName(for editor: EditorViewController, extension ext: String) -> String {
        if let url = editor.document.fileURL {
            let base = url.deletingPathExtension().lastPathComponent
            return "\(base).\(ext)"
        }
        return "Untitled.\(ext)"
    }

    @objc private func makeRichText(_ sender: Any?) {
        activeWindow()?.currentEditor?.setRichTextMode(true)
    }

    @objc private func makePlainText(_ sender: Any?) {
        guard let editor = activeWindow()?.currentEditor else { return }
        if editor.document.isRichText {
            let alert = NSAlert()
            alert.messageText = "Convert to plain text?"
            alert.informativeText = "All formatting will be removed. This cannot be undone."
            alert.addButton(withTitle: "Convert")
            alert.addButton(withTitle: "Cancel")
            guard alert.runModal() == .alertFirstButtonReturn else { return }
        }
        editor.setRichTextMode(false)
    }

    // MARK: - Other

    @objc private func showPreferences(_ sender: Any?) {
        if preferencesController == nil {
            preferencesController = PreferencesWindowController()
        }
        preferencesController?.showWindow(nil)
        preferencesController?.window?.makeKeyAndOrderFront(nil)
    }

    @objc private func showAbout(_ sender: Any?) {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.5.3"
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "TextPad",
            .applicationVersion: version,
            .credits: NSAttributedString(string: "A lightweight text editor for macOS.\nInspired by BBEdit and CotEditor.")
        ])
    }

    @objc private func openHelp(_ sender: Any?) {
        guard let url = AppHelp.url else {
            showError(
                "Help file not found",
                "The help file could not be found. Rebuild TextPad so Help.md is copied into the app bundle."
            )
            return
        }
        openFile(at: url)
    }

    private func handleSingleInstanceCommand(_ command: SingleInstanceCommand) {
        switch command {
        case .openFiles(let paths):
            for path in paths {
                openFile(at: URL(fileURLWithPath: path))
            }
            bringAllToFront(nil)
        case .activate:
            bringAllToFront(nil)
            NSApp.activate(ignoringOtherApps: true)
        }
    }

    @objc private func bringAllToFront(_ sender: Any?) {
        for wc in windows { wc.window?.makeKeyAndOrderFront(nil) }
    }

    private func showError(_ title: String, _ message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.runModal()
    }

    func validateMenuItem(_ menuItem: NSMenuItem) -> Bool {
        guard let action = menuItem.action else { return true }

        let editor = activeWindow()?.currentEditor
        let hasEditor = editor != nil

        switch action {
        case #selector(newDocument(_:)), #selector(openDocument(_:)),
             #selector(openWithEncoding(_:)),
             #selector(showPreferences(_:)), #selector(showAbout(_:)),
             #selector(openHelp(_:)),
             #selector(runPageLayout(_:)), #selector(bringAllToFront(_:)):
            return true
        case #selector(newTab(_:)), #selector(closeTab(_:)), #selector(closeWindow(_:)),
             #selector(saveDocumentAction(_:)), #selector(saveDocumentAsAction(_:)),
             #selector(printDocument(_:)),
             #selector(showFind(_:)), #selector(showFindReplace(_:)),
             #selector(findNext(_:)), #selector(findPrevious(_:)),
             #selector(reopenClosedTab(_:)),
             #selector(showGoToLine(_:)), #selector(zoomIn(_:)), #selector(zoomOut(_:)),
             #selector(toggleLineNumbers(_:)), #selector(toggleWordWrap(_:)),
             #selector(toggleInvisibles(_:)), #selector(toggleCurrentLineHighlight(_:)),
             #selector(setLanguage(_:)), #selector(toggleBoldface(_:)),
             #selector(toggleItalics(_:)), #selector(underline(_:)),
             #selector(strikethrough), #selector(showFonts(_:)),
             #selector(showTextColor(_:)), #selector(showHighlightColor(_:)),
             #selector(alignLeft(_:)), #selector(alignCenter(_:)),
             #selector(alignRight(_:)), #selector(alignJustified(_:)),
             #selector(pasteAndMatchStyle(_:)), #selector(toggleBulletList(_:)),
             #selector(toggleNumberedList(_:)), #selector(increaseIndent(_:)),
             #selector(decreaseIndent(_:)), #selector(makeRichText(_:)):
            return hasEditor
        case #selector(exportAsPDF(_:)), #selector(exportAsHTML(_:)):
            return hasEditor
        case #selector(makePlainText(_:)):
            return hasEditor && editor?.document.isRichText == true
        case #selector(revertDocument(_:)):
            return hasEditor && editor?.document.fileURL != nil
        case #selector(showDocumentEncoding(_:)):
            return hasEditor && editor?.document.isRichText == false
        default:
            return true
        }
    }
}

// MARK: - Helpers exposed to AppDelegate



