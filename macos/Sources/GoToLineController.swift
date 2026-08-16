import AppKit

final class GoToLineController: NSWindowController {
    private let lineField = NSTextField()
    weak var targetTextView: NSTextView?

    init() {
        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 300, height: 100),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        panel.title = "Go to Line"
        super.init(window: panel)
        setupUI()
    }

    required init?(coder: NSCoder) { fatalError() }

    private func setupUI() {
        guard let content = window?.contentView else { return }

        let label = NSTextField(labelWithString: "Line number:")
        lineField.placeholderString = "1"
        lineField.formatter = NumberFormatter()

        let goButton = NSButton(title: "Go", target: self, action: #selector(go))
        goButton.bezelStyle = .rounded
        goButton.keyEquivalent = "\r"

        let grid = NSGridView(views: [[label, lineField]])
        grid.column(at: 1).xPlacement = .fill

        let stack = NSStackView(views: [grid, goButton])
        stack.orientation = .vertical
        stack.spacing = 12
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.edgeInsets = NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16)

        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.centerXAnchor.constraint(equalTo: content.centerXAnchor),
            stack.topAnchor.constraint(equalTo: content.topAnchor),
            lineField.widthAnchor.constraint(equalToConstant: 120)
        ])
    }

    func showPanel(for textView: NSTextView) {
        targetTextView = textView
        lineField.stringValue = ""
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        lineField.becomeFirstResponder()
    }

    @objc private func go() {
        guard let textView = targetTextView else { return }
        let line = Int(lineField.stringValue) ?? 1
        guard line > 0 else { return }

        let text = textView.string as NSString
        let lineCount = max(1, Self.lineCount(in: text))
        let targetLine = min(line, lineCount)
        let charIndex = Self.offset(ofLine: targetLine, in: text)

        let range = NSRange(location: charIndex, length: 0)
        textView.setSelectedRange(range)
        textView.scrollRangeToVisible(range)
        close()
    }

    private static func lineCount(in text: NSString) -> Int {
        guard text.length > 0 else { return 1 }
        var count = 1
        var location = 0
        while location < text.length {
            location = NSMaxRange(text.lineRange(for: NSRange(location: location, length: 0)))
            if location < text.length {
                count += 1
            }
        }
        return count
    }

    private static func offset(ofLine line: Int, in text: NSString) -> Int {
        guard line > 1 else { return 0 }
        var location = 0
        var current = 1
        while current < line, location < text.length {
            location = NSMaxRange(text.lineRange(for: NSRange(location: location, length: 0)))
            current += 1
        }
        return min(location, text.length)
    }
}