import AppKit

protocol EditorViewControllerDelegate: AnyObject {
    func editorDidChange(_ controller: EditorViewController)
    func editorSelectionDidChange(_ controller: EditorViewController)
    func editorWantsClose(_ controller: EditorViewController) -> Bool
}

final class EditorViewController: NSViewController, NSTextViewDelegate {
    let document: EditorDocument
    weak var delegate: EditorViewControllerDelegate?

    private(set) var scrollView: NSScrollView!
    private(set) var textView: NSTextView!
    private var lineNumberGutter: LineNumberGutterView?
    private var lineHighlightLayoutManager: LineHighlightLayoutManager?
    private var statusBar: NSView!
    private var lineColLabel = NSTextField(labelWithString: "Ln 1, Col 1")
    private var languageLabel = NSTextField(labelWithString: "Plain Text")
    private var encodingLabel = NSTextField(labelWithString: "UTF-8")
    private var lineEndingLabel = NSTextField(labelWithString: "LF")
    private var charCountLabel = NSTextField(labelWithString: "0 characters")
    private var highlighter: SyntaxHighlighter?
    private let inWindowFindBar = InWindowFindBar()
    private var isFindBarVisible = false
    private let findBarHeight: CGFloat = 36
    private var fileChangeMonitor = FileChangeMonitor()
    private var autoSaveManager: AutoSaveManager!
    private var isPromptingForExternalChange = false

    init(document: EditorDocument) {
        self.document = document
        super.init(nibName: nil, bundle: nil)
        autoSaveManager = AutoSaveManager(editor: self)
    }

    deinit {
        NotificationCenter.default.removeObserver(self)
        fileChangeMonitor.stop()
        autoSaveManager.stop()
    }

    required init?(coder: NSCoder) { fatalError() }

    override func loadView() {
        let root = NSView(frame: NSRect(x: 0, y: 0, width: 900, height: 600))
        root.autoresizingMask = [.width, .height]

        let textStorage = NSTextStorage()
        let layoutManager = LineHighlightLayoutManager()
        let textContainer = NSTextContainer(containerSize: NSSize(width: 0, height: CGFloat.greatestFiniteMagnitude))
        textContainer.widthTracksTextView = true
        layoutManager.addTextContainer(textContainer)
        textStorage.addLayoutManager(layoutManager)
        lineHighlightLayoutManager = layoutManager

        scrollView = NSScrollView(frame: NSRect(x: 0, y: 22, width: 900, height: 578))
        scrollView.autoresizingMask = [.width, .height]
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.hasVerticalRuler = false
        scrollView.rulersVisible = false
        scrollView.borderType = .noBorder
        scrollView.drawsBackground = true
        let initialTheme = EditorPreferences.shared.effectiveTheme
        scrollView.backgroundColor = initialTheme.background

        let contentSize = scrollView.contentSize
        textView = NSTextView(
            frame: NSRect(x: 0, y: 0, width: contentSize.width, height: contentSize.height),
            textContainer: textContainer
        )
        layoutManager.highlightTextView = textView
        scrollView.documentView = textView
        LargeFileSupport.configureScrollable(
            textView,
            scrollView: scrollView,
            wordWrap: EditorPreferences.shared.wordWrap
        )
        textView.delegate = self
        textView.isEditable = true
        textView.isSelectable = true
        textView.allowsUndo = true
        textView.drawsBackground = true
        textView.backgroundColor = initialTheme.background
        textView.textColor = initialTheme.text
        textView.insertionPointColor = initialTheme.text
        textView.font = NSFont.monospacedSystemFont(ofSize: 13, weight: .regular)
        textView.textContainerInset = NSSize(width: 8, height: 8)
        RichTextFormatting.configure(textView, richText: false)

        if let storage = textView.textStorage {
            let highlighter = SyntaxHighlighter(storage: storage)
            highlighter.attach(textView: textView)
            self.highlighter = highlighter
        }

        scrollView.contentView.postsBoundsChangedNotifications = true
        NotificationCenter.default.addObserver(
            self, selector: #selector(editorScrolled),
            name: NSView.boundsDidChangeNotification, object: scrollView.contentView
        )

        statusBar = NSView(frame: NSRect(x: 0, y: 0, width: 900, height: 22))
        statusBar.autoresizingMask = [.width, .maxYMargin]
        statusBar.wantsLayer = true

        lineColLabel.frame = NSRect(x: 10, y: 4, width: 90, height: 14)
        languageLabel.frame = NSRect(x: 105, y: 4, width: 90, height: 14)
        encodingLabel.frame = NSRect(x: 200, y: 4, width: 95, height: 14)
        lineEndingLabel.frame = NSRect(x: 300, y: 4, width: 50, height: 14)
        charCountLabel.frame = NSRect(x: 520, y: 4, width: 370, height: 14)
        charCountLabel.alignment = .right
        charCountLabel.autoresizingMask = [.minXMargin]

        for label in [lineColLabel, languageLabel, encodingLabel, lineEndingLabel, charCountLabel] {
            label.font = NSFont.systemFont(ofSize: 11)
            label.textColor = initialTheme.chromeText
            label.isBezeled = false
            label.isEditable = false
            label.drawsBackground = false
            statusBar.addSubview(label)
        }

        inWindowFindBar.isHidden = true
        inWindowFindBar.delegate = self
        root.addSubview(scrollView)
        root.addSubview(inWindowFindBar)
        root.addSubview(statusBar)
        view = root

        NotificationCenter.default.addObserver(
            self, selector: #selector(preferencesChanged),
            name: EditorPreferences.didChangeNotification, object: nil
        )
    }

    override func viewDidLayout() {
        super.viewDidLayout()
        layoutEditorSubviews()
    }

    private func layoutEditorSubviews() {
        let w = view.bounds.width
        let h = view.bounds.height
        guard w > 0, h > 22 else { return }

        statusBar.frame = NSRect(x: 0, y: 0, width: w, height: 22)
        let topInset = isFindBarVisible ? findBarHeight : 0
        inWindowFindBar.frame = NSRect(x: 0, y: h - 22 - topInset, width: w, height: findBarHeight)
        inWindowFindBar.isHidden = !isFindBarVisible

        let editorBottom = 22 + topInset
        let editorHeight = h - editorBottom
        let showGutter = EditorPreferences.shared.showLineNumbers

        if showGutter, let gutter = lineNumberGutter {
            gutter.frame = NSRect(x: 0, y: editorBottom, width: LineNumberGutterView.width, height: editorHeight)
            gutter.isHidden = false
            scrollView.frame = NSRect(x: LineNumberGutterView.width, y: editorBottom, width: w - LineNumberGutterView.width, height: editorHeight)
        } else {
            lineNumberGutter?.isHidden = true
            scrollView.frame = NSRect(x: 0, y: editorBottom, width: w, height: editorHeight)
        }

        if EditorPreferences.shared.wordWrap, let container = textView.textContainer {
            let width = scrollView.contentSize.width
            if width > 0 {
                container.containerSize = NSSize(width: width, height: CGFloat.greatestFiniteMagnitude)
            }
        }
    }

    func reloadFromDocument() {
        _ = view

        let theme = EditorPreferences.shared.effectiveTheme
        RichTextFormatting.configure(textView, richText: document.isRichText)

        if document.isRichText, let rtf = document.rtfData,
           let attributed = NSAttributedString(rtf: rtf, documentAttributes: nil) {
            textView.textStorage?.setAttributedString(attributed)
        } else if document.isRichText {
            let attrs = RichTextFormatting.defaultTypingAttributes(theme: theme)
            textView.textStorage?.setAttributedString(
                NSAttributedString(string: document.content, attributes: attrs)
            )
        } else {
            textView.font = EditorPreferences.shared.font
            textView.textColor = theme.text
            textView.string = document.content
        }

        textView.backgroundColor = theme.background
        textView.insertionPointColor = theme.text
        scrollView.backgroundColor = theme.background

        let initialWrap = LargeFileSupport.effectiveWordWrap(
            preferred: EditorPreferences.shared.wordWrap,
            text: document.content
        )
        applyPreferences(wordWrap: initialWrap)
        LargeFileSupport.updateSizeToFitContent(textView: textView, scrollView: scrollView)
        updateStatusBar()
        textView.scrollRangeToVisible(NSRange(location: 0, length: 0))
        view.window?.makeFirstResponder(textView)
        textView.needsDisplay = true
        scrollView.needsDisplay = true
        configureFileMonitoring()
        autoSaveManager.start()
    }

    func syncDocument() {
        document.content = textView.string
        if document.isRichText {
            document.rtfData = RichTextFormatting.rtfData(from: textView)
        } else {
            document.rtfData = nil
        }
    }

    @objc func preferencesChanged() {
        applyPreferences()
        autoSaveManager.stop()
        autoSaveManager.start()
    }

    @objc private func editorScrolled() {
        refreshCurrentLineHighlight()
        highlighter?.visibleRangeDidChange()
    }

    func applyPreferences(wordWrap override: Bool? = nil) {
        let prefs = EditorPreferences.shared
        let theme = prefs.effectiveTheme
        inWindowFindBar.applyTheme(theme)

        RichTextFormatting.configure(textView, richText: document.isRichText)
        textView.backgroundColor = theme.background
        textView.insertionPointColor = theme.text
        scrollView.backgroundColor = theme.background
        scrollView.drawsBackground = true

        if document.isRichText {
            RichTextFormatting.applyTheme(theme, to: textView)
        } else {
            textView.font = prefs.font
            textView.textColor = theme.text
            textView.typingAttributes = [
                .font: prefs.font,
                .foregroundColor: theme.text
            ]
            textView.selectedTextAttributes = [
                .backgroundColor: theme.selection,
                .foregroundColor: theme.text
            ]
            PlainTextEditing.applyTabWidth(to: textView, font: prefs.font, tabWidth: prefs.tabWidth)
            PlainTextEditing.configureInvisibles(on: textView, show: prefs.showInvisibles)

            if let storage = textView.textStorage, storage.length > 0 {
                let range = NSRange(location: 0, length: storage.length)
                storage.addAttributes([
                    .font: prefs.font,
                    .foregroundColor: theme.text
                ], range: range)
            }
        }

        let wordWrap = override ?? prefs.wordWrap
        LargeFileSupport.configureScrollable(textView, scrollView: scrollView, wordWrap: wordWrap)

        if prefs.showLineNumbers {
            if lineNumberGutter == nil {
                let gutter = LineNumberGutterView(textView: textView, scrollView: scrollView)
                lineNumberGutter = gutter
                view.addSubview(gutter, positioned: .below, relativeTo: scrollView)
            }
            lineNumberGutter?.theme = theme
            lineNumberGutter?.isHidden = false
            lineNumberGutter?.needsDisplay = true
        } else {
            lineNumberGutter?.isHidden = true
        }

        scrollView.hasVerticalRuler = false
        scrollView.rulersVisible = false
        scrollView.verticalRulerView = nil

        layoutEditorSubviews()

        LargeFileSupport.updateSizeToFitContent(textView: textView, scrollView: scrollView)
        textView.needsDisplay = true

        if document.isRichText {
            highlighter?.configure(language: .plain, theme: theme)
            lineHighlightLayoutManager?.isHighlightEnabled = false
            PlainTextEditing.configureInvisibles(on: textView, show: false)
        } else {
            highlighter?.configure(language: document.language, theme: theme)
            lineHighlightLayoutManager?.isHighlightEnabled = prefs.showCurrentLineHighlight
            lineHighlightLayoutManager?.highlightColor = theme.currentLineHighlight
        }
        refreshCurrentLineHighlight()
        statusBar.layer?.backgroundColor = theme.lineNumberBackground.cgColor
        for label in [lineColLabel, languageLabel, encodingLabel, lineEndingLabel, charCountLabel] {
            label.textColor = theme.chromeText
        }
        updateStatusBar()
    }

    func updateStatusBar() {
        let text = textView.string as NSString
        let location = min(textView.selectedRange().location, text.length)
        let line = Self.lineNumber(at: location, in: text)
        let lineStart = text.lineRange(for: NSRange(location: location, length: 0)).location
        let col = location - lineStart + 1

        lineColLabel.stringValue = "Ln \(line), Col \(col)"
        languageLabel.stringValue = document.isRichText ? "Rich Text" : document.language.displayName
        encodingLabel.stringValue = document.isRichText ? "RTF" : TextEncoding.named(document.encoding)
        lineEndingLabel.stringValue = document.isRichText ? "" : document.lineEnding.displayName
        charCountLabel.stringValue = "\(text.length) characters"
    }

    func documentDidSave() {
        autoSaveManager.clearSnapshot()
        document.noteSavedToDisk()
        fileChangeMonitor.suppressBriefly()
        configureFileMonitoring()
    }

    func textDidChange(_ notification: Notification) {
        let editedRange = (notification.userInfo?["NSEditedRange"] as? NSValue)?.rangeValue ?? NSRange(location: 0, length: 0)
        let delta = (notification.userInfo?["NSDeltaLength"] as? NSNumber)?.intValue ?? 0

        if !document.isDirty {
            document.isDirty = true
            delegate?.editorDidChange(self)
        }

        if !document.isRichText {
            document.updateLineEndingIfNeeded(editedRange: editedRange, delta: delta, in: textView.string)
            highlighter?.textDidChange(editedRange: editedRange, delta: delta)
        }

        updateStatusBar()
        lineNumberGutter?.needsDisplay = true
        refreshCurrentLineHighlight()
    }

    private static func lineNumber(at location: Int, in text: NSString) -> Int {
        guard location > 0 else { return 1 }
        var line = 1
        var index = 0
        while index < location {
            let codeUnit = text.character(at: index)
            if codeUnit == 0x0A {
                line += 1
            } else if codeUnit == 0x0D {
                let next = index + 1
                if next >= location || text.character(at: next) != 0x0A {
                    line += 1
                }
            }
            index += 1
        }
        return line
    }

    func textViewDidChangeSelection(_ notification: Notification) {
        updateStatusBar()
        refreshCurrentLineHighlight()
        delegate?.editorSelectionDidChange(self)
    }

    func textView(_ textView: NSTextView, shouldChangeTextIn affectedCharRange: NSRange, replacementString: String?) -> Bool {
        guard !document.isRichText, let replacement = replacementString else { return true }
        guard PlainTextEditing.shouldHandlePairing(replacement) else { return true }
        return PlainTextEditing.handlePairing(replacement, in: textView, range: affectedCharRange)
    }

    func textView(_ textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
        if document.isRichText {
            if commandSelector == #selector(NSStandardKeyBindingResponding.insertTab(_:)) {
                RichTextFormatting.adjustIndent(in: textView, deltaLevels: 1)
                return true
            }
            if commandSelector == #selector(NSStandardKeyBindingResponding.insertBacktab(_:)) {
                RichTextFormatting.adjustIndent(in: textView, deltaLevels: -1)
                return true
            }
            return false
        }

        if commandSelector == #selector(NSStandardKeyBindingResponding.insertTab(_:)) {
            PlainTextEditing.insertTab(in: textView)
            return true
        }
        if commandSelector == #selector(NSStandardKeyBindingResponding.insertBacktab(_:)) {
            PlainTextEditing.insertBacktab(in: textView)
            return true
        }
        if commandSelector == #selector(NSStandardKeyBindingResponding.insertNewline(_:)) {
            PlainTextEditing.insertNewlineWithAutoIndent(in: textView)
            return true
        }
        return false
    }

    private func refreshCurrentLineHighlight() {
        guard !document.isRichText else { return }
        lineHighlightLayoutManager?.invalidateCurrentLineHighlight()
    }

    private func configureFileMonitoring() {
        fileChangeMonitor.stop()
        guard let url = document.fileURL else { return }
        fileChangeMonitor.watch(url: url) { [weak self] in
            self?.handleExternalFileChange()
        }
    }

    private func handleExternalFileChange() {
        guard !isPromptingForExternalChange, document.hasChangedOnDisk() else { return }
        isPromptingForExternalChange = true

        let alert = NSAlert()
        alert.messageText = "File changed on disk"
        alert.informativeText = "\"\(document.displayName.replacingOccurrences(of: " •", with: ""))\" was modified by another application."
        alert.addButton(withTitle: "Reload")
        alert.addButton(withTitle: "Keep Current Version")
        let response = alert.runModal()
        isPromptingForExternalChange = false

        if response == .alertFirstButtonReturn {
            do {
                try document.reloadFromDisk()
                reloadFromDocument()
                document.noteSavedToDisk()
                fileChangeMonitor.suppressBriefly()
                delegate?.editorDidChange(self)
            } catch {
                let errorAlert = NSAlert()
                errorAlert.messageText = "Could not reload file"
                errorAlert.informativeText = error.localizedDescription
                errorAlert.runModal()
            }
        } else if !document.isDirty {
            document.isDirty = true
            delegate?.editorDidChange(self)
        }
    }

    var activeTextView: NSTextView { textView }

    func showFindBar() {
        isFindBarVisible = true
        inWindowFindBar.applyTheme(EditorPreferences.shared.effectiveTheme)
        let selection = textView.selectedRange()
        let selected = (textView.string as NSString).substring(with: selection)
        let seed = selection.length > 0 && !selected.contains("\n") ? selected : inWindowFindBar.query
        inWindowFindBar.prepare(with: seed)
        layoutEditorSubviews()
    }

    func hideFindBar() {
        isFindBarVisible = false
        layoutEditorSubviews()
        view.window?.makeFirstResponder(textView)
    }

    func findNext() {
        if !isFindBarVisible {
            showFindBar()
            return
        }
        performFind(query: inWindowFindBar.query, options: inWindowFindBar.searchOptions, forward: true)
    }

    func findPrevious() {
        if !isFindBarVisible {
            showFindBar()
            return
        }
        performFind(query: inWindowFindBar.query, options: inWindowFindBar.searchOptions, forward: false)
    }

    private func performFind(query: String, options: TextSearchOptions, forward: Bool) {
        guard !query.isEmpty else {
            inWindowFindBar.setStatus("")
            return
        }

        let content = textView.string as NSString
        let start = forward
            ? textView.selectedRange().location + textView.selectedRange().length
            : max(0, textView.selectedRange().location - 1)

        guard let range = TextSearch.find(query, in: content, from: start, forward: forward, options: options) else {
            inWindowFindBar.setStatus("Not found")
            NSSound.beep()
            return
        }

        textView.setSelectedRange(range)
        textView.scrollRangeToVisible(range)
        textView.showFindIndicator(for: range)

        let total = TextSearch.countMatches(query, in: content, options: options)
        let index = TextSearch.matchNumber(at: range, for: query, in: content, options: options)
        inWindowFindBar.setStatus(total > 0 ? "\(index) of \(total)" : "Found")
    }

    func zoomIn() { EditorPreferences.shared.fontSize += 1 }
    func zoomOut() { EditorPreferences.shared.fontSize = max(8, EditorPreferences.shared.fontSize - 1) }

    func setLanguage(_ language: SyntaxLanguage) {
        guard !document.isRichText else { return }
        document.language = language
        applyPreferences()
    }

    func setRichTextMode(_ enabled: Bool) {
        syncDocument()
        if enabled && document.format == .plainText {
            document.format = .richText
            document.language = .plain
            let theme = EditorPreferences.shared.effectiveTheme
            let attrs = RichTextFormatting.defaultTypingAttributes(theme: theme)
            textView.textStorage?.setAttributedString(
                NSAttributedString(string: document.content, attributes: attrs)
            )
            RichTextFormatting.applyTheme(theme, to: textView)
            document.rtfData = RichTextFormatting.rtfData(from: textView)
        } else if !enabled && document.format == .richText {
            document.format = .plainText
            document.content = textView.string
            document.rtfData = nil
            document.lineEnding = LineEnding.detect(in: document.content)
            document.lineEndingPolicy = EditorPreferences.shared.lineEndingOnSave
            document.language = SyntaxLanguage.detect(from: document.fileURL)
        }
        reloadFromDocument()
        delegate?.editorDidChange(self)
    }
}

extension EditorViewController: InWindowFindBarDelegate {
    func findBar(_ bar: InWindowFindBar, didSearch query: String, options: TextSearchOptions, forward: Bool) {
        performFind(query: query, options: options, forward: forward)
    }

    func findBarDidClose(_ bar: InWindowFindBar) {
        hideFindBar()
    }
}