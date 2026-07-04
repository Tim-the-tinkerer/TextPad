import AppKit

final class DocumentWindowController: NSWindowController, EditorViewControllerDelegate, NSWindowDelegate {
    private var editors: [EditorViewController] = []
    private var currentIndex = 0
    private var editorContainer = DropReceivingView()
    private var tabBarContainer = NSView()

    var currentEditor: EditorViewController? {
        guard currentIndex >= 0, currentIndex < editors.count else { return nil }
        return editors[currentIndex]
    }

    init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 900, height: 650),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.minSize = NSSize(width: 400, height: 300)
        window.setFrameAutosaveName("TextPadMainWindow")
        super.init(window: window)
        window.delegate = self
        setupUI()
    }

    required init?(coder: NSCoder) { fatalError() }

    private func setupUI() {
        guard let content = window?.contentView else { return }

        tabBarContainer.frame = NSRect(x: 0, y: content.bounds.height - 32, width: content.bounds.width, height: 32)
        tabBarContainer.autoresizingMask = [.width, .minYMargin]

        editorContainer.frame = NSRect(x: 0, y: 0, width: content.bounds.width, height: content.bounds.height - 32)
        editorContainer.autoresizingMask = [.width, .height]

        content.addSubview(editorContainer)
        content.addSubview(tabBarContainer)

        editorContainer.onFilesDropped = { urls in
            let delegate = NSApp.delegate as? AppDelegate
            for url in urls {
                delegate?.openFile(at: url)
            }
        }

        NotificationCenter.default.addObserver(
            self, selector: #selector(handleWindowResize),
            name: NSWindow.didResizeNotification, object: window
        )
        NotificationCenter.default.addObserver(
            self, selector: #selector(preferencesChanged),
            name: EditorPreferences.didChangeNotification, object: nil
        )

        tabBarContainer.wantsLayer = true
        applyTabBarTheme()
    }

    @objc private func preferencesChanged() {
        refreshAppearance()
    }

    func refreshAppearance() {
        applyTabBarTheme()
        refreshTabBar()
    }

    private func applyTabBarTheme() {
        let theme = EditorPreferences.shared.effectiveTheme
        tabBarContainer.layer?.backgroundColor = theme.tabBarBackground.cgColor
    }

    private func styledTabTitle(_ title: String, selected: Bool) -> NSAttributedString {
        let theme = EditorPreferences.shared.effectiveTheme
        let color = selected ? theme.tabTextSelected : theme.tabText
        let weight: NSFont.Weight = selected ? .semibold : .regular
        return NSAttributedString(string: title, attributes: [
            .foregroundColor: color,
            .font: NSFont.systemFont(ofSize: 12, weight: weight)
        ])
    }

    private func styledChromeTitle(_ title: String, size: CGFloat, weight: NSFont.Weight = .medium) -> NSAttributedString {
        let theme = EditorPreferences.shared.effectiveTheme
        return NSAttributedString(string: title, attributes: [
            .foregroundColor: theme.tabText,
            .font: NSFont.systemFont(ofSize: size, weight: weight)
        ])
    }

    @objc private func handleWindowResize() {
        layoutContainers()
    }

    private func layoutContainers() {
        guard let content = window?.contentView else { return }
        let w = content.bounds.width
        let h = content.bounds.height
        tabBarContainer.frame = NSRect(x: 0, y: h - 32, width: w, height: 32)
        editorContainer.frame = NSRect(x: 0, y: 0, width: w, height: h - 32)

        if let editorView = currentEditor?.view {
            editorView.frame = editorContainer.bounds
        }
    }

    @discardableResult
    func openDocument(_ document: EditorDocument) -> EditorViewController {
        let editor = EditorViewController(document: document)
        editor.delegate = self
        editors.append(editor)
        showEditor(at: editors.count - 1)
        layoutContainers()
        refreshTabBar()
        updateWindowTitle()
        return editor
    }

    func newUntitledDocument() {
        openDocument(EditorDocument())
    }

    func hasSingleEmptyUntitledTab() -> Bool {
        guard editors.count == 1,
              let editor = editors.first,
              editor.document.fileURL == nil,
              !editor.document.isDirty,
              editor.document.content.isEmpty else { return false }
        return true
    }

    func replaceWithDocument(_ document: EditorDocument) {
        guard let editor = editors.first else {
            openDocument(document)
            return
        }
        editor.document.fileURL = document.fileURL
        editor.document.content = document.content
        editor.document.encoding = document.encoding
        editor.document.language = document.language
        editor.document.format = document.format
        editor.document.rtfData = document.rtfData
        editor.document.lineEnding = document.lineEnding
        editor.document.lineEndingPolicy = document.lineEndingPolicy
        editor.document.documentID = document.documentID
        editor.document.isDirty = document.isDirty
        showEditor(at: 0)
        editor.reloadFromDocument()
        refreshTabBar()
        updateWindowTitle()
    }

    private func showEditor(at index: Int) {
        guard index >= 0, index < editors.count else { return }
        currentIndex = index

        editorContainer.subviews.forEach { $0.removeFromSuperview() }

        let editor = editors[index]
        let editorView = editor.view
        editorView.frame = editorContainer.bounds
        editorView.autoresizingMask = [.width, .height]
        editorContainer.addSubview(editorView)

        editor.reloadFromDocument()
        window?.makeFirstResponder(editor.activeTextView)
    }

    func closeTab(at index: Int) {
        guard index >= 0, index < editors.count else { return }
        let editor = editors[index]
        if !confirmClose(editor: editor) { return }

        editor.syncDocument()
        ClosedTabManager.shared.recordClosed(document: editor.document)
        editors.remove(at: index)

        if editors.isEmpty {
            window?.close()
        } else {
            let nextIndex = min(index, editors.count - 1)
            showEditor(at: nextIndex)
            layoutContainers()
            refreshTabBar()
            updateWindowTitle()
        }
    }

    func closeCurrentTab() {
        closeTab(at: currentIndex)
    }

    func closeWindowWithSaveChecks() {
        window?.close()
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        for editor in editors {
            if !confirmClose(editor: editor) { return false }
        }
        return true
    }

    func selectTab(at index: Int) {
        showEditor(at: index)
        layoutContainers()
        refreshTabBar()
        updateWindowTitle()
    }

    func updateTabLabels() {
        refreshTabBar()
    }

    private func refreshTabBar() {
        tabBarContainer.subviews.forEach { $0.removeFromSuperview() }

        let stack = NSStackView()
        stack.orientation = .horizontal
        stack.alignment = .centerY
        stack.distribution = .fill
        stack.spacing = 4
        stack.translatesAutoresizingMaskIntoConstraints = false

        for (i, editor) in editors.enumerated() {
            let tabRow = NSStackView()
            tabRow.orientation = .horizontal
            tabRow.spacing = 2
            tabRow.setHuggingPriority(.required, for: .horizontal)

            let isSelected = currentIndex == i
            let btn = NSButton(title: "", target: self, action: #selector(tabClicked(_:)))
            btn.tag = i
            btn.bezelStyle = .accessoryBar
            btn.setButtonType(.toggle)
            btn.state = isSelected ? .on : .off
            btn.attributedTitle = styledTabTitle(editor.document.displayName, selected: isSelected)
            btn.contentTintColor = isSelected ? EditorPreferences.shared.effectiveTheme.tabTextSelected : EditorPreferences.shared.effectiveTheme.tabText
            btn.setContentHuggingPriority(.required, for: .horizontal)
            tabRow.addArrangedSubview(btn)

            let closeBtn = NSButton(title: "", target: self, action: #selector(closeTabClicked(_:)))
            closeBtn.tag = i
            closeBtn.bezelStyle = .inline
            closeBtn.isBordered = false
            closeBtn.attributedTitle = styledChromeTitle("×", size: 14)
            closeBtn.contentTintColor = EditorPreferences.shared.effectiveTheme.tabText
            closeBtn.toolTip = "Close Tab"
            closeBtn.setContentHuggingPriority(.required, for: .horizontal)
            tabRow.addArrangedSubview(closeBtn)

            stack.addArrangedSubview(tabRow)
        }

        let newTabBtn = NSButton(title: "", target: self, action: #selector(newTab))
        newTabBtn.bezelStyle = .accessoryBar
        newTabBtn.attributedTitle = styledChromeTitle("+", size: 14)
        newTabBtn.contentTintColor = EditorPreferences.shared.effectiveTheme.tabText
        newTabBtn.setContentHuggingPriority(.required, for: .horizontal)
        stack.addArrangedSubview(newTabBtn)

        stack.setHuggingPriority(.required, for: .horizontal)
        stack.setContentCompressionResistancePriority(.required, for: .horizontal)

        tabBarContainer.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: tabBarContainer.leadingAnchor, constant: 8),
            stack.centerYAnchor.constraint(equalTo: tabBarContainer.centerYAnchor)
        ])
    }

    @objc private func tabClicked(_ sender: NSButton) {
        selectTab(at: sender.tag)
    }

    @objc private func closeTabClicked(_ sender: NSButton) {
        closeTab(at: sender.tag)
    }

    @objc private func newTab() {
        newUntitledDocument()
    }

    func updateWindowTitle() {
        window?.title = currentEditor?.document.windowTitle ?? "TextPad"
        window?.representedURL = currentEditor?.document.fileURL
        window?.isDocumentEdited = currentEditor?.document.isDirty ?? false
    }

    func confirmClose(editor: EditorViewController) -> Bool {
        guard editor.document.isDirty else { return true }
        let alert = NSAlert()
        alert.messageText = "Save changes to \"\(editor.document.displayName)\"?"
        alert.informativeText = "Your changes will be lost if you don't save them."
        alert.addButton(withTitle: "Save")
        alert.addButton(withTitle: "Don't Save")
        alert.addButton(withTitle: "Cancel")
        let response = alert.runModal()
        switch response {
        case .alertFirstButtonReturn:
            return (NSApp.delegate as? AppDelegate)?.saveEditor(editor: editor) ?? false
        case .alertSecondButtonReturn:
            return true
        default:
            return false
        }
    }

    func editorDidChange(_ controller: EditorViewController) {
        refreshTabBar()
        updateWindowTitle()
    }

    func editorSelectionDidChange(_ controller: EditorViewController) {}

    func editorWantsClose(_ controller: EditorViewController) -> Bool {
        confirmClose(editor: controller)
    }

}