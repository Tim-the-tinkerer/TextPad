import AppKit

enum PlainTextEditing {
    private static let pairs: [Character: Character] = [
        "(": ")",
        "[": "]",
        "{": "}",
        "\"": "\"",
        "'": "'"
    ]

    static func applyTabWidth(to textView: NSTextView, font: NSFont, tabWidth: Int) {
        let charWidth = (" " as NSString).size(withAttributes: [.font: font]).width
        let interval = charWidth * CGFloat(max(1, tabWidth))

        let paragraph = NSMutableParagraphStyle()
        paragraph.defaultTabInterval = interval
        paragraph.tabStops = []

        textView.font = font
        var typing = textView.typingAttributes
        typing[.font] = font
        typing[.paragraphStyle] = paragraph
        textView.typingAttributes = typing

        if let storage = textView.textStorage, storage.length > 0 {
            storage.addAttribute(.paragraphStyle, value: paragraph, range: NSRange(location: 0, length: storage.length))
        }
    }

    static func configureInvisibles(on textView: NSTextView, show: Bool) {
        textView.layoutManager?.showsInvisibleCharacters = show
    }

    static func insertTab(in textView: NSTextView) {
        let prefs = EditorPreferences.shared
        let range = textView.selectedRange()

        if range.length > 0 {
            let spaces = String(repeating: " ", count: prefs.tabWidth)
            textView.insertText(spaces, replacementRange: range)
            return
        }

        let text = textView.string as NSString
        let lineRange = text.lineRange(for: NSRange(location: range.location, length: 0))
        let column = range.location - lineRange.location
        let remainder = column % prefs.tabWidth
        let spacesToNextStop = remainder == 0 ? prefs.tabWidth : (prefs.tabWidth - remainder)
        textView.insertText(String(repeating: " ", count: spacesToNextStop), replacementRange: range)
    }

    static func insertBacktab(in textView: NSTextView) {
        let prefs = EditorPreferences.shared
        let range = textView.selectedRange()
        let text = textView.string as NSString

        if range.length > 0 {
            dedentLines(in: textView, range: range, tabWidth: prefs.tabWidth)
            return
        }

        guard range.location > 0 else { return }
        let lineRange = text.lineRange(for: NSRange(location: range.location, length: 0))
        let column = range.location - lineRange.location
        guard column > 0 else { return }

        var removeCount = column % prefs.tabWidth
        if removeCount == 0 { removeCount = prefs.tabWidth }
        removeCount = min(removeCount, column)

        let slice = text.substring(with: NSRange(location: range.location - removeCount, length: removeCount))
        guard slice.unicodeScalars.allSatisfy({ $0 == " " }) else { return }

        textView.insertText("", replacementRange: NSRange(location: range.location - removeCount, length: removeCount))
    }

    static func insertNewlineWithAutoIndent(in textView: NSTextView) {
        let prefs = EditorPreferences.shared
        let range = textView.selectedRange()
        let text = textView.string as NSString
        let lineRange = text.lineRange(for: NSRange(location: range.location, length: 0))
        let line = text.substring(with: lineRange)
        let indent = String(line.prefix(while: { $0 == " " || $0 == "\t" }))

        let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
        var extraIndent = ""
        if trimmed.hasSuffix("{") || trimmed.hasSuffix("(") || trimmed.hasSuffix("[") || trimmed.hasSuffix(":") {
            extraIndent = String(repeating: " ", count: prefs.tabWidth)
        }

        textView.insertText("\n" + indent + extraIndent, replacementRange: range)
    }

    static func shouldHandlePairing(_ replacement: String) -> Bool {
        guard replacement.count == 1, let char = replacement.first else { return false }
        return pairs.keys.contains(char) || pairs.values.contains(char)
    }

    static func handlePairing(_ replacement: String, in textView: NSTextView, range: NSRange) -> Bool {
        guard replacement.count == 1, let char = replacement.first else { return true }
        let text = textView.string as NSString

        if let close = pairs[char] {
            if char == close {
                if range.location < text.length,
                   UnicodeScalar(text.character(at: range.location))?.description == String(close) {
                    textView.setSelectedRange(NSRange(location: range.location + 1, length: 0))
                    return false
                }
                if range.length == 0 {
                    textView.insertText(String(char) + String(close), replacementRange: range)
                    textView.setSelectedRange(NSRange(location: range.location + 1, length: 0))
                    return false
                }
            } else if range.length == 0 {
                textView.insertText(String(char) + String(close), replacementRange: range)
                textView.setSelectedRange(NSRange(location: range.location + 1, length: 0))
                return false
            }
            return true
        }

        if pairs.values.contains(char), range.length == 0, range.location < text.length,
           UnicodeScalar(text.character(at: range.location))?.description == String(char) {
            textView.setSelectedRange(NSRange(location: range.location + 1, length: 0))
            return false
        }

        return true
    }

    private static func dedentLines(in textView: NSTextView, range: NSRange, tabWidth: Int) {
        let text = textView.string as NSString
        let lineRange = text.paragraphRange(for: range)
        var removeRanges: [NSRange] = []

        var index = lineRange.location
        let end = NSMaxRange(lineRange)
        while index < end {
            let paraRange = text.paragraphRange(for: NSRange(location: index, length: 0))
            guard paraRange.length > 0 else { break }

            let prefix = text.substring(with: NSRange(location: paraRange.location, length: min(paraRange.length, tabWidth)))
            let removable = min(prefix.prefix(while: { $0 == " " }).count, tabWidth)
            if removable > 0 {
                removeRanges.append(NSRange(location: paraRange.location, length: removable))
            }
            index = NSMaxRange(paraRange)
        }

        guard !removeRanges.isEmpty, let storage = textView.textStorage else { return }
        storage.beginEditing()
        for removeRange in removeRanges.reversed() {
            storage.replaceCharacters(in: removeRange, with: "")
        }
        storage.endEditing()
    }
}