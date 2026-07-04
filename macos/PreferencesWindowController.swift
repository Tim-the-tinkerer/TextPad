import AppKit

final class PreferencesWindowController: NSWindowController {
    private let fontPopUp = NSPopUpButton()
    private let fontSizeField = NSTextField()
    private let tabWidthField = NSTextField()
    private let themePopUp = NSPopUpButton()
    private let lineNumbersCheckbox = NSButton(checkboxWithTitle: "Show line numbers", target: nil, action: nil)
    private let wordWrapCheckbox = NSButton(checkboxWithTitle: "Word wrap", target: nil, action: nil)
    private let invisiblesCheckbox = NSButton(checkboxWithTitle: "Show invisible characters", target: nil, action: nil)
    private let currentLineCheckbox = NSButton(checkboxWithTitle: "Highlight current line", target: nil, action: nil)
    private let autoSaveCheckbox = NSButton(checkboxWithTitle: "Auto-save documents", target: nil, action: nil)
    private let autoSaveIntervalField = NSTextField()
    private let defaultEncodingPopUp = NSPopUpButton()
    private let lineEndingPopUp = NSPopUpButton()

    private var scrollView: NSScrollView!
    private var rowLabels: [NSTextField] = []
    private var sectionTitles: [NSTextField] = []
    private var checkboxes: [NSButton] = []
    private var textFields: [NSTextField] = []
    private var popups: [NSPopUpButton] = []
    private var applyButton: NSButton!

    init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 440, height: 520),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        window.title = "Preferences"
        window.minSize = NSSize(width: 400, height: 420)
        super.init(window: window)
        setupUI()
        loadValues()
        applyTheme(previewTheme(for: EditorPreferences.shared.theme))

        NotificationCenter.default.addObserver(
            self,
            selector: #selector(preferencesDidChange),
            name: EditorPreferences.didChangeNotification,
            object: nil
        )
        DistributedNotificationCenter.default.addObserver(
            self,
            selector: #selector(systemAppearanceChanged),
            name: NSNotification.Name("AppleInterfaceThemeChangedNotification"),
            object: nil
        )
    }

    required init?(coder: NSCoder) { fatalError() }

    deinit {
        NotificationCenter.default.removeObserver(self)
        DistributedNotificationCenter.default.removeObserver(self)
    }

    override func showWindow(_ sender: Any?) {
        super.showWindow(sender)
        loadValues()
        applyTheme(previewTheme(for: EditorPreferences.shared.theme))
    }

    private func setupUI() {
        guard let content = window?.contentView else { return }
        content.wantsLayer = true

        let fonts = ["Menlo", "SF Mono", "Monaco", "Courier New", "Andale Mono", "Source Code Pro"]
        fontPopUp.addItems(withTitles: fonts)

        themePopUp.addItems(withTitles: EditorTheme.allCases.map(\.displayName))
        themePopUp.target = self
        themePopUp.action = #selector(themePopUpChanged)

        defaultEncodingPopUp.addItems(withTitles: TextEncoding.supported.map(\.name))
        lineEndingPopUp.addItems(withTitles: LineEndingPolicy.allCases.map(\.displayName))

        popups = [fontPopUp, themePopUp, defaultEncodingPopUp, lineEndingPopUp]
        for popup in popups {
            popup.setContentHuggingPriority(.defaultLow, for: .horizontal)
            popup.setContentCompressionResistancePriority(.required, for: .horizontal)
        }

        configureNumericField(fontSizeField)
        configureNumericField(tabWidthField)
        configureNumericField(autoSaveIntervalField)
        textFields = [fontSizeField, tabWidthField, autoSaveIntervalField]

        let fontLabel = makeLabel("Font:")
        let sizeLabel = makeLabel("Size:")
        let tabLabel = makeLabel("Tab width:")
        let themeLabel = makeLabel("Theme:")
        let encodingLabel = makeLabel("Default encoding:")
        let lineEndingLabel = makeLabel("Line endings on save:")
        let autoSaveIntervalLabel = makeLabel("Auto-save every (sec):")
        rowLabels = [fontLabel, sizeLabel, tabLabel, themeLabel, encodingLabel, lineEndingLabel, autoSaveIntervalLabel]

        let grid = NSGridView(views: [
            [fontLabel, fontPopUp],
            [sizeLabel, fontSizeField],
            [tabLabel, tabWidthField],
            [themeLabel, themePopUp],
            [encodingLabel, defaultEncodingPopUp],
            [lineEndingLabel, lineEndingPopUp],
            [autoSaveIntervalLabel, autoSaveIntervalField]
        ])
        grid.rowSpacing = 10
        grid.columnSpacing = 12
        grid.column(at: 0).xPlacement = .trailing
        grid.column(at: 0).width = 168
        grid.column(at: 1).xPlacement = .leading
        grid.translatesAutoresizingMaskIntoConstraints = false

        let editorSection = makeSection(title: "Editor", content: grid)

        checkboxes = [lineNumbersCheckbox, wordWrapCheckbox, invisiblesCheckbox, currentLineCheckbox, autoSaveCheckbox]
        let checkboxStack = NSStackView(views: checkboxes)
        checkboxStack.orientation = .vertical
        checkboxStack.alignment = .leading
        checkboxStack.spacing = 8
        checkboxStack.translatesAutoresizingMaskIntoConstraints = false

        let displaySection = makeSection(title: "Display", content: checkboxStack)

        applyButton = NSButton(title: "Apply", target: self, action: #selector(apply))
        applyButton.bezelStyle = .rounded
        applyButton.keyEquivalent = "\r"
        applyButton.translatesAutoresizingMaskIntoConstraints = false

        let stack = NSStackView(views: [editorSection, displaySection, applyButton])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 18
        stack.translatesAutoresizingMaskIntoConstraints = false

        scrollView = NSScrollView()
        scrollView.translatesAutoresizingMaskIntoConstraints = false
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.autohidesScrollers = true
        scrollView.borderType = .noBorder
        scrollView.drawsBackground = true
        scrollView.documentView = stack

        content.addSubview(scrollView)
        NSLayoutConstraint.activate([
            scrollView.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 16),
            scrollView.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -16),
            scrollView.topAnchor.constraint(equalTo: content.topAnchor, constant: 16),
            scrollView.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -16),

            stack.leadingAnchor.constraint(equalTo: scrollView.contentView.leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: scrollView.contentView.trailingAnchor),
            stack.topAnchor.constraint(equalTo: scrollView.contentView.topAnchor),
            stack.bottomAnchor.constraint(equalTo: scrollView.contentView.bottomAnchor, constant: -8),
            stack.widthAnchor.constraint(equalTo: scrollView.contentView.widthAnchor),

            fontPopUp.widthAnchor.constraint(equalToConstant: 240),
            themePopUp.widthAnchor.constraint(equalToConstant: 240),
            defaultEncodingPopUp.widthAnchor.constraint(equalToConstant: 240),
            lineEndingPopUp.widthAnchor.constraint(equalToConstant: 240),
            fontSizeField.widthAnchor.constraint(equalToConstant: 72),
            tabWidthField.widthAnchor.constraint(equalToConstant: 72),
            autoSaveIntervalField.widthAnchor.constraint(equalToConstant: 72)
        ])
    }

    private func makeLabel(_ text: String) -> NSTextField {
        let label = NSTextField(labelWithString: text)
        label.alignment = .right
        return label
    }

    private func configureNumericField(_ field: NSTextField) {
        field.isBezeled = true
        field.isEditable = true
        field.drawsBackground = true
        field.focusRingType = .default
        field.alignment = .right
        let formatter = NumberFormatter()
        formatter.allowsFloats = false
        formatter.minimum = 1
        field.formatter = formatter
    }

    private func makeSection(title: String, content: NSView) -> NSStackView {
        let titleLabel = NSTextField(labelWithString: title)
        titleLabel.font = NSFont.boldSystemFont(ofSize: 13)
        sectionTitles.append(titleLabel)

        let section = NSStackView(views: [titleLabel, content])
        section.orientation = .vertical
        section.alignment = .leading
        section.spacing = 10
        section.translatesAutoresizingMaskIntoConstraints = false
        return section
    }

    private func previewTheme(for selection: EditorTheme) -> EditorTheme {
        selection == .system ? EditorTheme.systemResolved : selection
    }

    private func applyTheme(_ theme: EditorTheme) {
        window?.backgroundColor = theme.background
        window?.appearance = theme.isDark ? NSAppearance(named: .darkAqua) : NSAppearance(named: .aqua)

        if let content = window?.contentView {
            content.layer?.backgroundColor = theme.background.cgColor
        }
        scrollView.backgroundColor = theme.background

        for label in rowLabels {
            label.textColor = theme.uiLabel
        }
        for title in sectionTitles {
            title.textColor = theme.uiSectionTitle
        }

        for field in textFields {
            styleTextField(field, theme: theme)
        }

        for popup in popups {
            stylePopup(popup, theme: theme)
        }

        for checkbox in checkboxes {
            styleCheckbox(checkbox, theme: theme)
        }

        styleApplyButton(applyButton, theme: theme)
    }

    private func styleTextField(_ field: NSTextField, theme: EditorTheme) {
        field.textColor = theme.uiLabel
        field.backgroundColor = theme.uiControlBackground
        field.wantsLayer = true
        field.layer?.borderColor = theme.uiControlBorder.cgColor
        field.layer?.borderWidth = 1
        field.layer?.cornerRadius = 4
    }

    private func stylePopup(_ popup: NSPopUpButton, theme: EditorTheme) {
        popup.contentTintColor = theme.uiLabel
        popup.wantsLayer = true
        popup.layer?.backgroundColor = theme.uiControlBackground.cgColor
        popup.layer?.borderColor = theme.uiControlBorder.cgColor
        popup.layer?.borderWidth = 1
        popup.layer?.cornerRadius = 6
    }

    private func styleCheckbox(_ checkbox: NSButton, theme: EditorTheme) {
        let title = checkbox.title
        checkbox.attributedTitle = NSAttributedString(
            string: title,
            attributes: [
                .foregroundColor: theme.uiLabel,
                .font: NSFont.systemFont(ofSize: NSFont.systemFontSize)
            ]
        )
    }

    private func styleApplyButton(_ button: NSButton, theme: EditorTheme) {
        button.contentTintColor = .white
        button.wantsLayer = true
        button.layer?.backgroundColor = theme.uiAccent.cgColor
        button.layer?.cornerRadius = 6
        button.isBordered = false
        button.attributedTitle = NSAttributedString(
            string: button.title,
            attributes: [
                .foregroundColor: NSColor.white,
                .font: NSFont.systemFont(ofSize: NSFont.systemFontSize, weight: .semibold)
            ]
        )
    }

    @objc private func themePopUpChanged() {
        let index = themePopUp.indexOfSelectedItem
        guard index >= 0, index < EditorTheme.allCases.count else { return }
        applyTheme(previewTheme(for: EditorTheme.allCases[index]))
    }

    @objc private func preferencesDidChange() {
        applyTheme(EditorPreferences.shared.effectiveTheme)
    }

    @objc private func systemAppearanceChanged() {
        guard EditorPreferences.shared.theme == .system else { return }
        applyTheme(EditorPreferences.shared.effectiveTheme)
    }

    private func loadValues() {
        let prefs = EditorPreferences.shared
        fontPopUp.selectItem(withTitle: prefs.fontName)
        fontSizeField.stringValue = "\(Int(prefs.fontSize))"
        tabWidthField.stringValue = "\(prefs.tabWidth)"
        themePopUp.selectItem(at: EditorTheme.allCases.firstIndex(of: prefs.theme) ?? 0)
        lineNumbersCheckbox.state = prefs.showLineNumbers ? .on : .off
        wordWrapCheckbox.state = prefs.wordWrap ? .on : .off
        invisiblesCheckbox.state = prefs.showInvisibles ? .on : .off
        currentLineCheckbox.state = prefs.showCurrentLineHighlight ? .on : .off
        autoSaveCheckbox.state = prefs.autoSaveEnabled ? .on : .off
        autoSaveIntervalField.stringValue = "\(prefs.autoSaveInterval)"
        if let idx = TextEncoding.supported.firstIndex(where: { $0.encoding == prefs.defaultEncoding }) {
            defaultEncodingPopUp.selectItem(at: idx)
        }
        if let idx = LineEndingPolicy.allCases.firstIndex(of: prefs.lineEndingOnSave) {
            lineEndingPopUp.selectItem(at: idx)
        }
    }

    @objc private func apply() {
        let prefs = EditorPreferences.shared
        if let font = fontPopUp.titleOfSelectedItem { prefs.fontName = font }
        prefs.fontSize = CGFloat(Int(fontSizeField.stringValue) ?? 13)
        prefs.tabWidth = Int(tabWidthField.stringValue) ?? 4
        let themeIndex = themePopUp.indexOfSelectedItem
        if themeIndex >= 0, themeIndex < EditorTheme.allCases.count {
            prefs.theme = EditorTheme.allCases[themeIndex]
        }
        prefs.showLineNumbers = lineNumbersCheckbox.state == .on
        prefs.wordWrap = wordWrapCheckbox.state == .on
        prefs.showInvisibles = invisiblesCheckbox.state == .on
        prefs.showCurrentLineHighlight = currentLineCheckbox.state == .on
        prefs.autoSaveEnabled = autoSaveCheckbox.state == .on
        prefs.autoSaveInterval = Int(autoSaveIntervalField.stringValue) ?? 60
        if defaultEncodingPopUp.indexOfSelectedItem >= 0,
           defaultEncodingPopUp.indexOfSelectedItem < TextEncoding.supported.count {
            prefs.defaultEncoding = TextEncoding.supported[defaultEncodingPopUp.indexOfSelectedItem].encoding
        }
        if lineEndingPopUp.indexOfSelectedItem >= 0,
           lineEndingPopUp.indexOfSelectedItem < LineEndingPolicy.allCases.count {
            prefs.lineEndingOnSave = LineEndingPolicy.allCases[lineEndingPopUp.indexOfSelectedItem]
        }
        applyTheme(EditorPreferences.shared.effectiveTheme)
    }
}