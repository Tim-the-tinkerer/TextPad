import AppKit

final class EncodingOptionsController: NSWindowController {
    enum Mode {
        case open
        case document(EditorDocument)
    }

    private let encodingPopUp = NSPopUpButton()
    private let lineEndingPopUp = NSPopUpButton()
    private let statusLabel = NSTextField(labelWithString: "")
    private let mode: Mode
    private var completion: ((String.Encoding, LineEndingPolicy) -> Void)?

    init(mode: Mode) {
        self.mode = mode
        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 360, height: 180),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        switch mode {
        case .open: panel.title = "Open with Encoding"
        case .document: panel.title = "Document Encoding"
        }
        super.init(window: panel)
        setupUI()
        loadValues()
    }

    required init?(coder: NSCoder) { fatalError() }

    private var modeTitle: String {
        switch mode {
        case .open: return "Open with Encoding"
        case .document: return "Document Encoding"
        }
    }

    private func setupUI() {
        guard let content = window?.contentView else { return }

        for item in TextEncoding.supported {
            encodingPopUp.addItem(withTitle: item.name)
        }
        for policy in LineEndingPolicy.allCases {
            lineEndingPopUp.addItem(withTitle: policy.displayName)
        }

        let encodingLabel = NSTextField(labelWithString: "Encoding:")
        let lineEndingLabel = NSTextField(labelWithString: "Line endings on save:")

        let grid = NSGridView(views: [
            [encodingLabel, encodingPopUp],
            [lineEndingLabel, lineEndingPopUp]
        ])
        grid.column(at: 0).xPlacement = .trailing
        grid.column(at: 1).xPlacement = .fill

        statusLabel.font = NSFont.systemFont(ofSize: 11)
        statusLabel.textColor = .secondaryLabelColor
        statusLabel.lineBreakMode = .byWordWrapping
        statusLabel.maximumNumberOfLines = 2

        let applyButton = NSButton(title: applyButtonTitle, target: self, action: #selector(apply))
        applyButton.bezelStyle = .rounded
        applyButton.keyEquivalent = "\r"

        let stack = NSStackView(views: [grid, statusLabel, applyButton])
        stack.orientation = .vertical
        stack.alignment = .centerX
        stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.edgeInsets = NSEdgeInsets(top: 18, left: 18, bottom: 18, right: 18)

        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            stack.topAnchor.constraint(equalTo: content.topAnchor),
            encodingPopUp.widthAnchor.constraint(equalToConstant: 200),
            lineEndingPopUp.widthAnchor.constraint(equalToConstant: 200)
        ])
    }

    private var applyButtonTitle: String {
        switch mode {
        case .open: return "Open"
        case .document: return "Apply"
        }
    }

    private func loadValues() {
        switch mode {
        case .open:
            if let idx = TextEncoding.supported.firstIndex(where: { $0.encoding == EditorPreferences.shared.defaultEncoding }) {
                encodingPopUp.selectItem(at: idx)
            }
            if let idx = LineEndingPolicy.allCases.firstIndex(of: EditorPreferences.shared.lineEndingOnSave) {
                lineEndingPopUp.selectItem(at: idx)
            }
            statusLabel.stringValue = "Choose how to decode the file."
        case .document(let document):
            if let idx = TextEncoding.supported.firstIndex(where: { $0.encoding == document.encoding }) {
                encodingPopUp.selectItem(at: idx)
            }
            if let idx = LineEndingPolicy.allCases.firstIndex(of: document.lineEndingPolicy) {
                lineEndingPopUp.selectItem(at: idx)
            }
            statusLabel.stringValue = "Detected line endings: \(document.lineEnding.displayName)"
        }
    }

    func show(completion: @escaping (String.Encoding, LineEndingPolicy) -> Void) {
        self.completion = completion
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        encodingPopUp.becomeFirstResponder()
    }

    @objc private func apply() {
        let encodingIndex = encodingPopUp.indexOfSelectedItem
        let policyIndex = lineEndingPopUp.indexOfSelectedItem
        guard encodingIndex >= 0, encodingIndex < TextEncoding.supported.count,
              policyIndex >= 0, policyIndex < LineEndingPolicy.allCases.count else { return }
        let encoding = TextEncoding.supported[encodingIndex].encoding
        let policy = LineEndingPolicy.allCases[policyIndex]
        completion?(encoding, policy)
        close()
    }
}