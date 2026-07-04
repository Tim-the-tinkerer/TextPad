import AppKit

enum LargeFileSupport {
    static let longLineThreshold = 8000
    static let largeDocumentThreshold = 500_000

    static func maxLineLength(in text: String) -> Int {
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
        preferred && maxLineLength(in: text) <= longLineThreshold
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
    }

    static func updateSizeToFitContent(textView: NSTextView, scrollView: NSScrollView) {
        guard let layoutManager = textView.layoutManager,
              let textContainer = textView.textContainer else { return }

        layoutManager.ensureLayout(for: textContainer)
        let used = layoutManager.usedRect(for: textContainer)
        let inset = textView.textContainerInset
        var size = scrollView.contentSize
        size.height = max(size.height, used.height + inset.height * 2)

        if textView.isHorizontallyResizable {
            size.width = max(size.width, used.width + inset.width * 2)
        } else {
            size.width = scrollView.contentSize.width
        }

        textView.setFrameSize(size)
    }
}