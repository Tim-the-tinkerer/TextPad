import AppKit

enum LargeFileSupport {
    static let longLineThreshold = 8000
    static let largeDocumentThreshold = 500_000

    static func maxLineLength(in text: String) -> Int {
        // Avoid a second full Unicode traversal just to decide whether wrapping
        // should be disabled for a document already known to be large.
        if text.utf16.count > largeDocumentThreshold {
            return longLineThreshold + 1
        }
        var maxLength = 0
        var current = 0
        for character in text {
            if character == "\n" {
                maxLength = max(maxLength, current)
                current = 0
            } else if character != "\r" {
                current += 1
            }
        }
        return max(maxLength, current)
    }

    static func effectiveWordWrap(preferred: Bool, text: String) -> Bool {
        _ = text
        return preferred
    }

    static func configureScrollable(_ textView: NSTextView, scrollView: NSScrollView, wordWrap: Bool) {
        let contentSize = scrollView.contentSize

        textView.isVerticallyResizable = true
        textView.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)

        if wordWrap {
            textView.textContainer?.widthTracksTextView = true
            textView.isHorizontallyResizable = false
            textView.minSize = NSSize(width: contentSize.width, height: contentSize.height)
            textView.textContainer?.containerSize = NSSize(
                width: max(contentSize.width, 1),
                height: CGFloat.greatestFiniteMagnitude
            )
            var frame = textView.frame
            frame.size.width = contentSize.width
            textView.setFrameSize(frame.size)
        } else {
            textView.textContainer?.widthTracksTextView = false
            textView.textContainer?.containerSize = NSSize(
                width: CGFloat.greatestFiniteMagnitude,
                height: CGFloat.greatestFiniteMagnitude
            )
            textView.isHorizontallyResizable = true
            textView.minSize = NSSize(width: contentSize.width, height: contentSize.height)
        }

        applyForcedWrap(to: textView, wordWrap: wordWrap)
    }

    /// When wrap is on, break at the window edge even mid-word so long tokens
    /// (JSON, minified JS, base64) stay in view instead of scrolling sideways.
    /// Rich text keeps the document's own paragraph wrapping.
    static func applyForcedWrap(to textView: NSTextView, wordWrap: Bool) {
        guard !textView.isRichText else { return }
        let mode: NSLineBreakMode = wordWrap ? .byCharWrapping : .byClipping
        let paragraph: NSMutableParagraphStyle
        if let existing = (textView.defaultParagraphStyle ?? textView.typingAttributes[.paragraphStyle] as? NSParagraphStyle)?
            .mutableCopy() as? NSMutableParagraphStyle
        {
            paragraph = existing
        } else {
            paragraph = NSMutableParagraphStyle()
        }
        paragraph.lineBreakMode = mode
        textView.defaultParagraphStyle = paragraph

        var typing = textView.typingAttributes
        typing[.paragraphStyle] = paragraph
        textView.typingAttributes = typing

        if let storage = textView.textStorage, storage.length > 0 {
            storage.addAttribute(.paragraphStyle, value: paragraph, range: NSRange(location: 0, length: storage.length))
        }
    }

    static func updateSizeToFitContent(textView: NSTextView, scrollView: NSScrollView) {
        // NSTextView lays out text incrementally. Forcing ensureLayout here made
        // opening a file proportional to the size of the entire document and
        // defeated AppKit's viewport-driven layout.
        var size = textView.frame.size
        size.height = max(size.height, scrollView.contentSize.height)
        if !textView.isHorizontallyResizable {
            size.width = scrollView.contentSize.width
        }
        textView.setFrameSize(size)
    }
}
