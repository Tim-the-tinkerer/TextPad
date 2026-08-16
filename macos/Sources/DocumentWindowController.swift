import AppKit

final class DocumentWindowController: NSWindowController, EditorViewControllerDelegate, NSWindowDelegate {
    private var editors: [EditorViewController] = []
    private var currentIndex = 0
    private var editorContainer = DropReceivingView()
    private var tabBarContainer = NSView()
    private let tabScrollView = HorizontalTabScrollView()
    private let newTabButton = NSButton(title: "", target: nil, action: nil)
    private let overflowLeftButton = NSButton(title: "", target: nil, action: nil)
    private let overflowRightButton = NSButton(title: "", target: nil, action: nil)
    private let tabBarHeight: CGFloat = 32
    private let tabTitleMaxWidth: CGFloat = 200
    private let tabScrollStep: CGFloat = 180
    private var overflowLeftWidth: NSLayoutConstraint!
    private var overflowRightWidth: NSLayoutConstraint!

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

        tabBarContainer.frame = NSRect(x: 0, y: content.bounds.height - tabBarHeight, width: content.bounds.width, height: tabBarHeight)
        tabBarContainer.autoresizingMask = [.width, .minYMargin]

        editorContainer.frame = NSRect(x: 0, y: 0, width: content.bounds.width, height: content.bounds.height - tabBarHeight)
        editorContainer.autoresizingMask = [.width, .height]

        content.addSubview(editorContainer)
        content.addSubview(tabBarContainer)
        setupTabBarChrome()

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

    private func setupTabBarChrome() {
        tabScrollView.drawsBackground = false
        tabScrollView.borderType = .noBorder
        tabScrollView.hasVerticalScroller = false
        tabScrollView.hasHorizontalScroller = false
        tabScrollView.autohidesScrollers = true
        tabScrollView.verticalScrollElasticity = .none
        tabScrollView.horizontalScrollElasticity = .allowed
        tabScrollView.automaticallyAdjustsContentInsets = false
        tabScrollView.contentInsets = .init()
        tabScrollView.scrollerInsets = .init()
        tabScrollView.translatesAutoresizingMaskIntoConstraints = false
        tabScrollView.contentView.postsBoundsChangedNotifications = true
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(tabStripDidScroll),
            name: NSView.boundsDidChangeNotification,
            object: tabScrollView.contentView
        )

        configureChromeButton(overflowLeftButton, title: "‹", tooltip: "Show earlier tabs", action: #selector(scrollTabsLeft))
        configureChromeButton(overflowRightButton, title: "›", tooltip: "Show later tabs", action: #selector(scrollTabsRight))
        configureChromeButton(newTabButton, title: "+", tooltip: "New Tab", action: #selector(newTab))

        overflowLeftWidth = overflowLeftButton.widthAnchor.constraint(equalToConstant: 0)
        overflowRightWidth = overflowRightButton.widthAnchor.constraint(equalToConstant: 0)

        tabBarContainer.addSubview(overflowLeftButton)
        tabBarContainer.addSubview(tabScrollView)
        tabBarContainer.addSubview(overflowRightButton)
        tabBarContainer.addSubview(newTabButton)
        NSLayoutConstraint.activate([
            overflowLeftButton.leadingAnchor.constraint(equalTo: tabBarContainer.leadingAnchor, constant: 4),
            overflowLeftButton.centerYAnchor.constraint(equalTo: tabBarContainer.centerYAnchor),
            overflowLeftWidth,
            tabScrollView.leadingAnchor.constraint(equalTo: overflowLeftButton.trailingAnchor, constant: 2),
            tabScrollView.topAnchor.constraint(equalTo: tabBarContainer.topAnchor),
            tabScrollView.bottomAnchor.constraint(equalTo: tabBarContainer.bottomAnchor),
            tabScrollView.trailingAnchor.constraint(equalTo: overflowRightButton.leadingAnchor, constant: -2),
            overflowRightButton.trailingAnchor.constraint(equalTo: newTabButton.leadingAnchor, constant: -2),
            overflowRightButton.centerYAnchor.constraint(equalTo: tabBarContainer.centerYAnchor),
            overflowRightWidth,
            newTabButton.trailingAnchor.constraint(equalTo: tabBarContainer.trailingAnchor, constant: -8),
            newTabButton.centerYAnchor.constraint(equalTo: tabBarContainer.centerYAnchor)
        ])
    }

    private func configureChromeButton(_ button: NSButton, title: String, tooltip: String, action: Selector) {
        button.target = self
        button.action = action
        button.bezelStyle = .accessoryBar
        button.toolTip = tooltip
        button.attributedTitle = styledChromeTitle(title, size: 14)
        button.setContentHuggingPriority(.required, for: .horizontal)
        button.setContentCompressionResistancePriority(.required, for: .horizontal)
        button.translatesAutoresizingMaskIntoConstraints = false
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
        newTabButton.attributedTitle = styledChromeTitle("+", size: 14)
        newTabButton.contentTintColor = theme.tabText
        overflowLeftButton.attributedTitle = styledChromeTitle("‹", size: 16)
        overflowRightButton.attributedTitle = styledChromeTitle("›", size: 16)
        overflowLeftButton.contentTintColor = theme.tabText
        overflowRightButton.contentTintColor = theme.tabText
    }

    private func styledTabTitle(_ title: String, selected: Bool) -> NSAttributedString {
        let theme = EditorPreferences.shared.effectiveTheme
        let color = selected ? theme.tabTextSelected : theme.tabText
        let weight: NSFont.Weight = selected ? .semibold : .regular
        let paragraph = NSMutableParagraphStyle()
        paragraph.lineBreakMode = .byTruncatingMiddle
        return NSAttributedString(string: title, attributes: [
            .foregroundColor: color,
            .font: NSFont.systemFont(ofSize: 12, weight: weight),
            .paragraphStyle: paragraph
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
        tabBarContainer.frame = NSRect(x: 0, y: h - tabBarHeight, width: w, height: tabBarHeight)
        editorContainer.frame = NSRect(x: 0, y: 0, width: w, height: h - tabBarHeight)

        if let editorView = currentEditor?.view {
            editorView.frame = editorContainer.bounds
        }
        sizeTabStrip()
        scrollSelectedTabIntoView()
        updateOverflowControls()
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
        editor.document.writesByteOrderMark = document.writesByteOrderMark
        editor.document.language = document.language
        editor.document.format = document.format
        editor.document.rtfData = document.rtfData
        editor.document.lineEnding = document.lineEnding
        editor.document.lineEndingPolicy = document.lineEndingPolicy
        editor.document.documentID = document.documentID
        editor.document.isDirty = document.isDirty
        showEditor(at: 0)
        editor.reloadFromDocument(force: true)
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
        applyTabBarTheme()

        let stack = NSStackView()
        stack.orientation = .horizontal
        stack.alignment = .centerY
        stack.distribution = .gravityAreas
        stack.spacing = 4
        stack.edgeInsets = NSEdgeInsets(top: 0, left: 8, bottom: 0, right: 8)

        for (i, editor) in editors.enumerated() {
            let tabRow = NSStackView()
            tabRow.orientation = .horizontal
            tabRow.spacing = 2
            tabRow.alignment = .centerY
            tabRow.setHuggingPriority(.defaultHigh, for: .horizontal)
            tabRow.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)

            let isSelected = currentIndex == i
            let title = editor.document.displayName
            let btn = NSButton(title: "", target: self, action: #selector(tabClicked(_:)))
            btn.tag = i
            btn.bezelStyle = .accessoryBar
            btn.setButtonType(.toggle)
            btn.state = isSelected ? .on : .off
            btn.attributedTitle = styledTabTitle(title, selected: isSelected)
            btn.contentTintColor = isSelected
                ? EditorPreferences.shared.effectiveTheme.tabTextSelected
                : EditorPreferences.shared.effectiveTheme.tabText
            btn.toolTip = title
            (btn.cell as? NSButtonCell)?.lineBreakMode = .byTruncatingMiddle
            (btn.cell as? NSButtonCell)?.wraps = false
            (btn.cell as? NSButtonCell)?.truncatesLastVisibleLine = true
            btn.setContentHuggingPriority(.defaultLow, for: .horizontal)
            btn.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
            btn.widthAnchor.constraint(lessThanOrEqualToConstant: tabTitleMaxWidth).isActive = true
            tabRow.addArrangedSubview(btn)

            let closeBtn = NSButton(title: "", target: self, action: #selector(closeTabClicked(_:)))
            closeBtn.tag = i
            closeBtn.bezelStyle = .inline
            closeBtn.isBordered = false
            closeBtn.attributedTitle = styledChromeTitle("×", size: 14)
            closeBtn.contentTintColor = EditorPreferences.shared.effectiveTheme.tabText
            closeBtn.toolTip = "Close Tab"
            closeBtn.setContentHuggingPriority(.required, for: .horizontal)
            closeBtn.setContentCompressionResistancePriority(.required, for: .horizontal)
            tabRow.addArrangedSubview(closeBtn)

            stack.addArrangedSubview(tabRow)
        }

        tabScrollView.documentView = stack
        sizeTabStrip()
        scrollSelectedTabIntoView()
        updateOverflowControls()
    }

    private func sizeTabStrip() {
        guard let stack = tabScrollView.documentView else { return }
        stack.layoutSubtreeIfNeeded()
        let fitted = stack.fittingSize
        let viewport = tabScrollView.contentView.bounds
        stack.setFrameSize(NSSize(
            width: max(fitted.width, viewport.width),
            height: tabBarHeight
        ))
    }

    private func scrollSelectedTabIntoView() {
        guard let stack = tabScrollView.documentView as? NSStackView,
              currentIndex >= 0,
              currentIndex < stack.arrangedSubviews.count else { return }
        let tab = stack.arrangedSubviews[currentIndex]
        tab.layoutSubtreeIfNeeded()
        tab.scrollToVisible(tab.bounds.insetBy(dx: -12, dy: 0))
    }

    @objc private func tabStripDidScroll() {
        updateOverflowControls()
    }

    @objc private func scrollTabsLeft() {
        scrollTabs(by: -tabScrollStep)
    }

    @objc private func scrollTabsRight() {
        scrollTabs(by: tabScrollStep)
    }

    private func scrollTabs(by delta: CGFloat) {
        let clip = tabScrollView.contentView
        var origin = clip.bounds.origin
        origin.x += delta
        let maxX = max(0, (tabScrollView.documentView?.frame.width ?? 0) - clip.bounds.width)
        origin.x = min(max(origin.x, 0), maxX)
        clip.scroll(to: origin)
        tabScrollView.reflectScrolledClipView(clip)
        updateOverflowControls()
    }

    private func updateOverflowControls() {
        let clip = tabScrollView.contentView.bounds
        let documentWidth = tabScrollView.documentView?.frame.width ?? 0
        let overflow = documentWidth > clip.width + 1
        overflowLeftButton.isHidden = !overflow
        overflowRightButton.isHidden = !overflow
        overflowLeftWidth.constant = overflow ? 22 : 0
        overflowRightWidth.constant = overflow ? 22 : 0
        overflowLeftButton.isEnabled = clip.minX > 1
        overflowRightButton.isEnabled = clip.maxX < documentWidth - 1
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

/// Horizontal tab strip. Vertical wheel / swipe moves sideways so overflow
/// tabs stay reachable without a permanent scrollbar.
private final class HorizontalTabScrollView: NSScrollView {
    override func scrollWheel(with event: NSEvent) {
        let clip = contentView
        var origin = clip.bounds.origin
        let delta = abs(event.scrollingDeltaX) >= abs(event.scrollingDeltaY)
            ? event.scrollingDeltaX
            : event.scrollingDeltaY
        origin.x -= event.hasPreciseScrollingDeltas ? delta : delta * 8
        let maxX = max(0, (documentView?.frame.width ?? 0) - clip.bounds.width)
        origin.x = min(max(origin.x, 0), maxX)
        clip.scroll(to: origin)
        reflectScrolledClipView(clip)
    }
}
