import AppKit

enum RichTextFormatting {
    static func configure(_ textView: NSTextView, richText: Bool) {
        textView.isRichText = richText
        textView.importsGraphics = false
        textView.allowsDocumentBackgroundColorChange = richText
        textView.usesFontPanel = richText
        textView.isAutomaticLinkDetectionEnabled = richText
        textView.isAutomaticDataDetectionEnabled = richText
        textView.isAutomaticTextReplacementEnabled = richText
        textView.isAutomaticQuoteSubstitutionEnabled = richText
        textView.isAutomaticDashSubstitutionEnabled = richText

        if richText {
            textView.font = NSFont.systemFont(ofSize: EditorPreferences.shared.fontSize)
        }
    }

    static func defaultTypingAttributes(theme: EditorTheme) -> [NSAttributedString.Key: Any] {
        [
            .font: NSFont.systemFont(ofSize: EditorPreferences.shared.fontSize),
            .foregroundColor: theme.text
        ]
    }

    static func applyTheme(_ theme: EditorTheme, to textView: NSTextView) {
        textView.backgroundColor = theme.background
        textView.drawsBackground = true
        textView.insertionPointColor = theme.text
        textView.textColor = theme.text
        textView.typingAttributes = defaultTypingAttributes(theme: theme)
        textView.selectedTextAttributes = [
            .backgroundColor: theme.selection,
            .foregroundColor: theme.text
        ]
        textView.linkTextAttributes = [
            .foregroundColor: theme.uiAccent,
            .underlineStyle: NSUnderlineStyle.single.rawValue
        ]

        guard let storage = textView.textStorage else { return }
        let fullRange = NSRange(location: 0, length: storage.length)
        guard fullRange.length > 0 else { return }

        storage.beginEditing()

        var index = 0
        while index < fullRange.length {
            var effectiveRange = NSRange()
            let existing = storage.attribute(.foregroundColor, at: index, effectiveRange: &effectiveRange)
            if existing == nil || shouldRemapForeground(existing as? NSColor, for: theme) {
                storage.addAttribute(.foregroundColor, value: theme.text, range: effectiveRange)
            }
            index = NSMaxRange(effectiveRange)
        }

        storage.enumerateAttribute(.backgroundColor, in: fullRange) { value, range, _ in
            guard let color = value as? NSColor, shouldRemapHighlight(color, for: theme) else { return }
            storage.addAttribute(.backgroundColor, value: theme.selection, range: range)
        }

        storage.enumerateAttribute(.link, in: fullRange) { value, range, _ in
            guard value != nil else { return }
            storage.addAttribute(.foregroundColor, value: theme.uiAccent, range: range)
        }

        storage.endEditing()
    }

    private static func shouldRemapForeground(_ color: NSColor?, for theme: EditorTheme) -> Bool {
        guard let rgb = (color ?? .black).usingColorSpace(.sRGB) else { return true }
        var r: CGFloat = 0
        var g: CGFloat = 0
        var b: CGFloat = 0
        var alpha: CGFloat = 0
        rgb.getRed(&r, green: &g, blue: &b, alpha: &alpha)

        let maxChannel = max(r, g, b)
        let minChannel = min(r, g, b)
        let saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel
        let luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b

        if saturation > 0.18 { return false }

        if theme.isDark {
            return luminance < 0.55
        }
        return luminance > 0.65
    }

    private static func shouldRemapHighlight(_ color: NSColor, for theme: EditorTheme) -> Bool {
        guard let rgb = color.usingColorSpace(.sRGB) else { return false }
        var r: CGFloat = 0
        var g: CGFloat = 0
        var b: CGFloat = 0
        var alpha: CGFloat = 0
        rgb.getRed(&r, green: &g, blue: &b, alpha: &alpha)
        let luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b

        if theme.isDark {
            return luminance < 0.22 && alpha > 0.2
        }
        return luminance > 0.92 && alpha > 0.2
    }

    static func rtfData(from textView: NSTextView) -> Data? {
        let length = textView.textStorage?.length ?? 0
        let range = NSRange(location: 0, length: max(0, length))
        if length == 0 {
            let empty = NSAttributedString(string: "", attributes: textView.typingAttributes)
            return try? empty.data(
                from: NSRange(location: 0, length: 0),
                documentAttributes: [.documentType: NSAttributedString.DocumentType.rtf]
            )
        }
        return textView.rtf(from: range)
    }

    static func toggleStrikethrough(in textView: NSTextView) {
        let range = textView.selectedRange()
        guard let storage = textView.textStorage else { return }

        func strikethroughStyle(at loc: Int) -> Int {
            storage.attribute(.strikethroughStyle, at: loc, effectiveRange: nil) as? Int ?? 0
        }

        let enable: Bool
        if range.length > 0 {
            enable = strikethroughStyle(at: range.location) == 0
        } else {
            enable = (textView.typingAttributes[.strikethroughStyle] as? Int ?? 0) == 0
        }

        if range.length > 0 {
            storage.beginEditing()
            if enable {
                storage.addAttribute(.strikethroughStyle, value: NSUnderlineStyle.single.rawValue, range: range)
            } else {
                storage.removeAttribute(.strikethroughStyle, range: range)
            }
            storage.endEditing()
        }

        var attrs = textView.typingAttributes
        if enable {
            attrs[.strikethroughStyle] = NSUnderlineStyle.single.rawValue
        } else {
            attrs.removeValue(forKey: .strikethroughStyle)
        }
        textView.typingAttributes = attrs
    }

    static func showTextColorPanel(for textView: NSTextView, controller: TextColorController) {
        controller.show(for: textView)
    }

    static func applyTextColor(_ color: NSColor, in textView: NSTextView) {
        let range = textView.selectedRange()
        guard let storage = textView.textStorage else { return }

        if range.length > 0 {
            storage.beginEditing()
            storage.addAttribute(.foregroundColor, value: color, range: range)
            storage.endEditing()
        }

        var attrs = textView.typingAttributes
        attrs[.foregroundColor] = color
        textView.typingAttributes = attrs
        textView.textColor = color
    }

    static func applyHighlightColor(_ color: NSColor, in textView: NSTextView) {
        let range = textView.selectedRange()
        guard let storage = textView.textStorage else { return }

        if range.length > 0 {
            storage.beginEditing()
            storage.addAttribute(.backgroundColor, value: color, range: range)
            storage.endEditing()
        }

        var attrs = textView.typingAttributes
        attrs[.backgroundColor] = color
        textView.typingAttributes = attrs
    }
}

final class TextColorController: NSObject {
    weak var textView: NSTextView?

    func show(for textView: NSTextView) {
        self.textView = textView
        let panel = NSColorPanel.shared
        panel.mode = .RGB
        panel.showsAlpha = true
        panel.isContinuous = true
        panel.setTarget(self)
        panel.setAction(#selector(applyColor(_:)))
        panel.color = (textView.typingAttributes[.foregroundColor] as? NSColor)
            ?? textView.textColor
            ?? .black
        panel.orderFront(nil)
    }

    @objc private func applyColor(_ sender: NSColorPanel) {
        guard let textView else { return }
        RichTextFormatting.applyTextColor(sender.color, in: textView)
    }
}

final class HighlightColorController: NSObject {
    weak var textView: NSTextView?

    func show(for textView: NSTextView) {
        self.textView = textView
        let panel = NSColorPanel.shared
        panel.mode = .RGB
        panel.showsAlpha = true
        panel.isContinuous = true
        panel.setTarget(self)
        panel.setAction(#selector(applyColor(_:)))
        panel.color = (textView.typingAttributes[.backgroundColor] as? NSColor)
            ?? .yellow.withAlphaComponent(0.35)
        panel.orderFront(nil)
    }

    @objc private func applyColor(_ sender: NSColorPanel) {
        guard let textView else { return }
        RichTextFormatting.applyHighlightColor(sender.color, in: textView)
    }
}

extension RichTextFormatting {
    enum ListStyle {
        case bullet
        case numbered

        var markerFormat: NSTextList.MarkerFormat {
            switch self {
            case .bullet: return .disc
            case .numbered: return .decimal
            }
        }
    }

    private static let listIndent: CGFloat = 24

    static func setAlignment(_ alignment: NSTextAlignment, in textView: NSTextView) {
        applyParagraphStyle(in: textView) { style in
            style.alignment = alignment
        }
    }

    static func toggleList(_ style: ListStyle, in textView: NSTextView) {
        guard let storage = textView.textStorage else { return }
        let string = storage.string as NSString

        if storage.length == 0 {
            updateTypingParagraphStyle(in: textView) { para in
                configureListParagraph(para, style: style)
            }
            return
        }

        let range = string.paragraphRange(for: textView.selectedRange())
        var shouldRemove = true

        string.enumerateSubstrings(in: range, options: .byParagraphs) { _, paraRange, _, _ in
            guard paraRange.length > 0, paraRange.location < storage.length else { return }
            let existing = storage.attribute(.paragraphStyle, at: paraRange.location, effectiveRange: nil) as? NSParagraphStyle
            guard let list = existing?.textLists.first, list.markerFormat == style.markerFormat else {
                shouldRemove = false
                return
            }
        }

        applyParagraphStyle(in: textView, range: range) { para in
            if shouldRemove {
                clearListParagraph(para)
            } else {
                configureListParagraph(para, style: style)
            }
        }
    }

    static func adjustIndent(in textView: NSTextView, deltaLevels: Int) {
        guard deltaLevels != 0 else { return }
        let delta = CGFloat(deltaLevels) * listIndent

        applyParagraphStyle(in: textView) { para in
            let newIndent = max(0, para.headIndent + delta)
            para.headIndent = newIndent
            if para.textLists.isEmpty {
                para.firstLineHeadIndent = newIndent
            }
        }
    }

    static func pasteAndMatchStyle(in textView: NSTextView) {
        let pasteboard = NSPasteboard.general
        guard let string = pasteboard.string(forType: .string) else {
            NSApp.sendAction(#selector(NSText.paste(_:)), to: nil, from: textView)
            return
        }

        let normalized = string
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        let attributed = NSAttributedString(string: normalized, attributes: textView.typingAttributes)
        textView.insertText(attributed, replacementRange: textView.selectedRange())
    }

    static func htmlData(from textView: NSTextView) throws -> Data {
        let storage = textView.textStorage ?? NSTextStorage()
        let range = NSRange(location: 0, length: storage.length)
        let documentAttributes: [NSAttributedString.DocumentAttributeKey: Any] = [
            .documentType: NSAttributedString.DocumentType.html,
            .characterEncoding: String.Encoding.utf8.rawValue
        ]
        let body = try storage.data(from: range, documentAttributes: documentAttributes)
        guard var html = String(data: body, encoding: .utf8) else {
            throw NSError(domain: "TextPad", code: 4, userInfo: [
                NSLocalizedDescriptionKey: "Unable to generate HTML."
            ])
        }

        if !html.lowercased().contains("<html") {
            html = """
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <title>Exported Document</title>
            </head>
            <body>
            \(html)
            </body>
            </html>
            """
        }
        guard let data = html.data(using: .utf8) else {
            throw NSError(domain: "TextPad", code: 4, userInfo: [
                NSLocalizedDescriptionKey: "Unable to encode HTML."
            ])
        }
        return data
    }

    static func pdfData(from textView: NSTextView) -> Data? {
        guard let layoutManager = textView.layoutManager,
              let container = textView.textContainer else { return nil }
        let used = layoutManager.usedRect(for: container)
        let width = max(used.width + textView.textContainerInset.width * 2 + 32, 612)
        let height = max(used.height + textView.textContainerInset.height * 2 + 32, 792)
        return textView.dataWithPDF(inside: NSRect(x: 0, y: 0, width: width, height: height))
    }

    private static func configureListParagraph(_ para: NSMutableParagraphStyle, style: ListStyle) {
        let textList = NSTextList(markerFormat: style.markerFormat, options: 0)
        para.textLists = [textList]
        para.headIndent = listIndent
        para.firstLineHeadIndent = 0
        para.defaultTabInterval = listIndent
    }

    private static func clearListParagraph(_ para: NSMutableParagraphStyle) {
        para.textLists = []
        para.headIndent = 0
        para.firstLineHeadIndent = 0
    }

    private static func applyParagraphStyle(
        in textView: NSTextView,
        range explicitRange: NSRange? = nil,
        transform: (NSMutableParagraphStyle) -> Void
    ) {
        guard let storage = textView.textStorage else { return }
        let string = storage.string as NSString
        let range = explicitRange ?? string.paragraphRange(for: textView.selectedRange())

        if storage.length == 0 {
            updateTypingParagraphStyle(in: textView, transform: transform)
            return
        }

        var typingStyle: NSMutableParagraphStyle?
        var paraIndex = range.location
        let end = NSMaxRange(range)

        storage.beginEditing()
        while paraIndex < end {
            let paraRange = string.paragraphRange(for: NSRange(location: paraIndex, length: 0))
            if paraRange.length > 0, paraRange.location < storage.length {
                let existing = storage.attribute(.paragraphStyle, at: paraRange.location, effectiveRange: nil) as? NSParagraphStyle
                let mutable = (existing?.mutableCopy() as? NSMutableParagraphStyle) ?? NSMutableParagraphStyle()
                transform(mutable)
                storage.addAttribute(.paragraphStyle, value: mutable, range: paraRange)
                typingStyle = mutable
            }
            paraIndex = NSMaxRange(paraRange)
            if paraRange.length == 0 { break }
        }
        storage.endEditing()

        if let typingStyle {
            updateTypingParagraphStyle(in: textView) { para in
                let merged = (para.mutableCopy() as? NSMutableParagraphStyle) ?? NSMutableParagraphStyle()
                merged.setParagraphStyle(typingStyle)
                transform(merged)
                para.setParagraphStyle(merged)
            }
        }
    }

    private static func updateTypingParagraphStyle(
        in textView: NSTextView,
        transform: (NSMutableParagraphStyle) -> Void
    ) {
        var attrs = textView.typingAttributes
        let para = (attrs[.paragraphStyle] as? NSParagraphStyle)?.mutableCopy() as? NSMutableParagraphStyle
            ?? NSMutableParagraphStyle()
        transform(para)
        attrs[.paragraphStyle] = para
        textView.typingAttributes = attrs
    }
}