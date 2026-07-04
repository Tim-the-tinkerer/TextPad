import AppKit

/// Standalone gutter drawn beside the text view (avoids NSRulerView layout bugs).
final class LineNumberGutterView: NSView {
    static let width: CGFloat = 48

    weak var textView: NSTextView?
    weak var sourceScrollView: NSScrollView?
    var theme: EditorTheme = .light {
        didSet { needsDisplay = true }
    }

    override var isFlipped: Bool { true }

    init(textView: NSTextView, scrollView: NSScrollView) {
        self.textView = textView
        self.sourceScrollView = scrollView
        super.init(frame: .zero)
        scrollView.contentView.postsBoundsChangedNotifications = true
        NotificationCenter.default.addObserver(
            self, selector: #selector(scrollDidChange),
            name: NSView.boundsDidChangeNotification,
            object: scrollView.contentView
        )
    }

    required init?(coder: NSCoder) { fatalError() }

    deinit {
        NotificationCenter.default.removeObserver(self)
    }

    @objc private func scrollDidChange() {
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let textView = textView,
              let layoutManager = textView.layoutManager,
              let textContainer = textView.textContainer,
              let scrollView = sourceScrollView else { return }

        theme.lineNumberBackground.setFill()
        dirtyRect.fill()

        let visibleRect = scrollView.documentVisibleRect
        let glyphRange = layoutManager.glyphRange(forBoundingRect: visibleRect, in: textContainer)
        let charRange = layoutManager.characterRange(forGlyphRange: glyphRange, actualGlyphRange: nil)
        let text = textView.string as NSString

        let font = NSFont.monospacedDigitSystemFont(ofSize: EditorPreferences.shared.fontSize - 1, weight: .regular)
        let attributes: [NSAttributedString.Key: Any] = [
            .font: font,
            .foregroundColor: theme.lineNumberText
        ]

        var lineNumber = 1
        if charRange.location > 0 {
            lineNumber = text.substring(to: charRange.location).components(separatedBy: "\n").count
        }

        var index = charRange.location
        let end = NSMaxRange(charRange)

        while index < end {
            let glyphIndex = layoutManager.glyphIndexForCharacter(at: index)
            let lineRect = layoutManager.lineFragmentRect(forGlyphAt: glyphIndex, effectiveRange: nil)
            let y = lineRect.origin.y + textView.textContainerOrigin.y - visibleRect.origin.y

            let label = "\(lineNumber)" as NSString
            let size = label.size(withAttributes: attributes)
            let x = bounds.width - size.width - 8
            let baseline = y + (lineRect.height - size.height) * 0.5
            label.draw(at: NSPoint(x: x, y: baseline), withAttributes: attributes)

            let nextNewline = text.lineRange(for: NSRange(location: index, length: 0))
            index = NSMaxRange(nextNewline)
            lineNumber += 1
        }
    }
}