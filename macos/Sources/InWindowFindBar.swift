import AppKit

protocol InWindowFindBarDelegate: AnyObject {
    func findBar(_ bar: InWindowFindBar, didSearch query: String, options: TextSearchOptions, forward: Bool)
    func findBarDidClose(_ bar: InWindowFindBar)
}

final class InWindowFindBar: NSView {
    weak var delegate: InWindowFindBarDelegate?

    private let findField = NSSearchField()
    private let prevButton = NSButton()
    private let nextButton = NSButton()
    private let caseButton = NSButton()
    private let statusLabel = NSTextField(labelWithString: "")
    private let closeButton = NSButton()

    var searchOptions: TextSearchOptions {
        var options = TextSearchOptions()
        options.caseSensitive = caseButton.state == .on
        return options
    }

    override var isFlipped: Bool { true }

    init() {
        super.init(frame: .zero)
        setupUI()
    }

    required init?(coder: NSCoder) { fatalError() }

    private func setupUI() {
        wantsLayer = true

        findField.placeholderString = "Find"
        findField.sendsSearchStringImmediately = true
        findField.sendsWholeSearchString = true
        findField.target = self
        findField.action = #selector(findFieldChanged)
        findField.focusRingType = .none

        prevButton.bezelStyle = .accessoryBar
        prevButton.image = NSImage(systemSymbolName: "chevron.left", accessibilityDescription: "Previous")
        prevButton.imagePosition = .imageOnly
        prevButton.target = self
        prevButton.action = #selector(findPrevious)
        prevButton.toolTip = "Find Previous"

        nextButton.bezelStyle = .accessoryBar
        nextButton.image = NSImage(systemSymbolName: "chevron.right", accessibilityDescription: "Next")
        nextButton.imagePosition = .imageOnly
        nextButton.target = self
        nextButton.action = #selector(findNext)
        nextButton.toolTip = "Find Next"

        caseButton.setButtonType(.toggle)
        caseButton.bezelStyle = .accessoryBar
        caseButton.title = "Aa"
        caseButton.toolTip = "Case Sensitive"
        caseButton.target = self
        caseButton.action = #selector(findFieldChanged)

        closeButton.bezelStyle = .accessoryBar
        closeButton.image = NSImage(systemSymbolName: "xmark", accessibilityDescription: "Close")
        closeButton.imagePosition = .imageOnly
        closeButton.target = self
        closeButton.action = #selector(closeBar)
        closeButton.toolTip = "Close"

        statusLabel.font = NSFont.systemFont(ofSize: 11)
        statusLabel.alignment = .right

        let controls = [findField, prevButton, nextButton, caseButton, statusLabel, closeButton]
        controls.forEach { $0.translatesAutoresizingMaskIntoConstraints = false }

        addSubview(findField)
        addSubview(prevButton)
        addSubview(nextButton)
        addSubview(caseButton)
        addSubview(statusLabel)
        addSubview(closeButton)

        NSLayoutConstraint.activate([
            findField.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 10),
            findField.centerYAnchor.constraint(equalTo: centerYAnchor),
            findField.trailingAnchor.constraint(equalTo: prevButton.leadingAnchor, constant: -8),

            prevButton.centerYAnchor.constraint(equalTo: centerYAnchor),
            prevButton.widthAnchor.constraint(equalToConstant: 28),
            prevButton.heightAnchor.constraint(equalToConstant: 24),

            nextButton.leadingAnchor.constraint(equalTo: prevButton.trailingAnchor, constant: 2),
            nextButton.centerYAnchor.constraint(equalTo: centerYAnchor),
            nextButton.widthAnchor.constraint(equalToConstant: 28),
            nextButton.heightAnchor.constraint(equalToConstant: 24),

            caseButton.leadingAnchor.constraint(equalTo: nextButton.trailingAnchor, constant: 8),
            caseButton.centerYAnchor.constraint(equalTo: centerYAnchor),
            caseButton.widthAnchor.constraint(equalToConstant: 32),

            closeButton.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -10),
            closeButton.centerYAnchor.constraint(equalTo: centerYAnchor),
            closeButton.widthAnchor.constraint(equalToConstant: 28),
            closeButton.heightAnchor.constraint(equalToConstant: 24),

            statusLabel.centerYAnchor.constraint(equalTo: centerYAnchor),
            statusLabel.trailingAnchor.constraint(equalTo: closeButton.leadingAnchor, constant: -8),
            statusLabel.widthAnchor.constraint(greaterThanOrEqualToConstant: 80)
        ])
    }

    func applyTheme(_ theme: EditorTheme) {
        layer?.backgroundColor = theme.tabBarBackground.cgColor
        findField.textColor = theme.text
        findField.backgroundColor = theme.uiControlBackground
        if let cell = findField.cell as? NSSearchFieldCell {
            cell.placeholderAttributedString = NSAttributedString(
                string: "Find",
                attributes: [.foregroundColor: theme.chromeText.withAlphaComponent(0.72)]
            )
        }
        statusLabel.textColor = theme.chromeText
        for button in [prevButton, nextButton, caseButton, closeButton] {
            button.contentTintColor = theme.tabTextSelected
        }
    }

    func prepare(with selection: String) {
        if !selection.isEmpty, !selection.contains("\n") {
            findField.stringValue = selection
        }
        findField.becomeFirstResponder()
        performSearch(forward: true)
    }

    func setStatus(_ text: String) {
        statusLabel.stringValue = text
    }

    var query: String { findField.stringValue }

    @objc private func findFieldChanged() {
        performSearch(forward: true)
    }

    @objc private func findNext() {
        performSearch(forward: true)
    }

    @objc private func findPrevious() {
        performSearch(forward: false)
    }

    @objc private func closeBar() {
        delegate?.findBarDidClose(self)
    }

    private func performSearch(forward: Bool) {
        delegate?.findBar(self, didSearch: findField.stringValue, options: searchOptions, forward: forward)
    }
}