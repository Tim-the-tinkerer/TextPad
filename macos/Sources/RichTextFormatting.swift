import AppKit

enum RichTextFormatting {
    static func configure(_ textView: NSTextView, richText: Bool) {
        textView.isRichText = richText
        textView.importsGraphics = richText
        textView.allowsDocumentBackgroundColorChange = richText
        textView.usesFontPanel = richText
        textView.isAutomaticLinkDetectionEnabled = richText
        textView.isAutomaticDataDetectionEnabled = richText
        textView.isAutomaticTextReplacementEnabled = richText
        textView.isAutomaticQuoteSubstitutionEnabled = richText
        textView.isAutomaticDashSubstitutionEnabled = richText

        // Never assign textView.font on a rich-text view that already has
        // content. NSTextView.font replaces every run — including named RTF
        // fonts such as Interlac Unicode — even when the selection is empty.
        if richText, (textView.textStorage?.length ?? 0) == 0 {
            textView.typingAttributes = defaultTypingAttributes(theme: EditorPreferences.shared.effectiveTheme)
        }
    }

    static func defaultTypingAttributes(theme: EditorTheme) -> [NSAttributedString.Key: Any] {
        [
            .font: NSFont.systemFont(ofSize: EditorPreferences.shared.fontSize),
            .foregroundColor: theme.text
        ]
    }

    static func applyTheme(_ theme: EditorTheme, to textView: NSTextView) {
        // Paint the RTF page with the application theme. Do not assign
        // textView.font or textView.textColor — both rewrite every run and
        // would discard the RTF font table. Display remapping uses layout
        // manager temporary attributes so stored RTF colors stay intact.
        textView.backgroundColor = theme.background
        textView.drawsBackground = true
        textView.insertionPointColor = theme.text
        textView.appearance = NSAppearance(named: theme.isDark ? .darkAqua : .aqua)
        textView.typingAttributes = typingAttributesPreservingDocument(in: textView, theme: theme)
        textView.selectedTextAttributes = [
            .backgroundColor: theme.selection,
            .foregroundColor: theme.text
        ]
        textView.linkTextAttributes = [
            .foregroundColor: theme.uiAccent,
            .underlineStyle: NSUnderlineStyle.single.rawValue
        ]
        applyDisplayColors(theme: theme, to: textView)
    }

    static func syncTypingAttributes(_ theme: EditorTheme, to textView: NSTextView) {
        textView.typingAttributes = typingAttributesPreservingDocument(in: textView, theme: theme)
    }

    static func refreshDisplayColors(_ theme: EditorTheme, to textView: NSTextView) {
        applyDisplayColors(theme: theme, to: textView)
        syncTypingAttributes(theme, to: textView)
    }

    private static let minimumReadableContrast: CGFloat = 3.0
    private static let displayColorKeys: [NSAttributedString.Key] = [
        .foregroundColor, .backgroundColor, .underlineColor
    ]

    private static func applyDisplayColors(theme: EditorTheme, to textView: NSTextView) {
        guard let storage = textView.textStorage, let layout = textView.layoutManager else { return }
        clearDisplayColors(in: textView)
        guard storage.length > 0 else { return }
        let full = NSRange(location: 0, length: storage.length)

        storage.enumerateAttributes(in: full, options: []) { attributes, run, _ in
            if attributes[.link] != nil {
                return
            }

            let backgroundLuminance = effectiveBackgroundLuminance(
                at: run.location,
                in: storage,
                editorBackground: theme.background
            )

            if let background = attributes[.backgroundColor] as? NSColor {
                if shouldRemapHighlight(background, for: theme) {
                    layout.addTemporaryAttribute(.backgroundColor, value: theme.selection, forCharacterRange: run)
                } else if shouldRemapDocumentBackground(background, for: theme) {
                    layout.addTemporaryAttribute(.backgroundColor, value: theme.background, forCharacterRange: run)
                }
            }

            let foreground = (attributes[.foregroundColor] as? NSColor) ?? .black
            let components = colorComponents(foreground)
            if shouldNormalizeForeground(components: components, backgroundLuminance: backgroundLuminance, theme: theme) {
                let ink = preferredTextColor(forBackgroundLuminance: backgroundLuminance, theme: theme)
                layout.addTemporaryAttribute(.foregroundColor, value: ink, forCharacterRange: run)
            } else if theme.isDark, shouldBrightenAccent(components, backgroundLuminance: backgroundLuminance) {
                layout.addTemporaryAttribute(.foregroundColor, value: brightenForDarkTheme(foreground), forCharacterRange: run)
            }

            if let underline = attributes[.underlineColor] as? NSColor {
                let underlineComponents = colorComponents(underline)
                if shouldNormalizeForeground(
                    components: underlineComponents,
                    backgroundLuminance: backgroundLuminance,
                    theme: theme
                ) {
                    layout.addTemporaryAttribute(
                        .underlineColor,
                        value: preferredTextColor(forBackgroundLuminance: backgroundLuminance, theme: theme),
                        forCharacterRange: run
                    )
                }
            }
        }
    }

    private static func clearDisplayColors(in textView: NSTextView) {
        guard let storage = textView.textStorage, let layout = textView.layoutManager else { return }
        let full = NSRange(location: 0, length: storage.length)
        for key in displayColorKeys {
            layout.removeTemporaryAttribute(key, forCharacterRange: full)
        }
    }

    private static func typingAttributesPreservingDocument(
        in textView: NSTextView,
        theme: EditorTheme
    ) -> [NSAttributedString.Key: Any] {
        guard let storage = textView.textStorage, storage.length > 0 else {
            return [
                .font: NSFont.systemFont(ofSize: EditorPreferences.shared.fontSize),
                .foregroundColor: theme.text
            ]
        }

        let location = min(max(textView.selectedRange().location, 0), storage.length - 1)
        var attributes = storage.attributes(at: location, effectiveRange: nil)
        if attributes[.font] == nil {
            attributes[.font] = NSFont.systemFont(ofSize: EditorPreferences.shared.fontSize)
        }

        let backgroundLuminance = effectiveBackgroundLuminance(
            at: location,
            in: storage,
            editorBackground: theme.background
        )
        let foreground = (attributes[.foregroundColor] as? NSColor) ?? .black
        let components = colorComponents(foreground)
        if shouldNormalizeForeground(components: components, backgroundLuminance: backgroundLuminance, theme: theme) {
            attributes[.foregroundColor] = preferredTextColor(forBackgroundLuminance: backgroundLuminance, theme: theme)
        } else if theme.isDark, shouldBrightenAccent(components, backgroundLuminance: backgroundLuminance) {
            attributes[.foregroundColor] = brightenForDarkTheme(foreground)
        } else if attributes[.foregroundColor] == nil {
            attributes[.foregroundColor] = theme.text
        }
        return attributes
    }

    private struct ColorComponents {
        let red: CGFloat
        let green: CGFloat
        let blue: CGFloat
        let alpha: CGFloat
        let saturation: CGFloat
        let luminance: CGFloat
    }

    private static func colorComponents(_ color: NSColor) -> ColorComponents {
        let rgb = color.usingColorSpace(.sRGB) ?? color
        var r: CGFloat = 0
        var g: CGFloat = 0
        var b: CGFloat = 0
        var alpha: CGFloat = 0
        rgb.getRed(&r, green: &g, blue: &b, alpha: &alpha)
        let maxChannel = max(r, g, b)
        let minChannel = min(r, g, b)
        let saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel
        let luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b
        return ColorComponents(
            red: r,
            green: g,
            blue: b,
            alpha: alpha,
            saturation: saturation,
            luminance: luminance
        )
    }

    private static func effectiveBackgroundLuminance(
        at index: Int,
        in storage: NSTextStorage,
        editorBackground: NSColor
    ) -> CGFloat {
        if let background = storage.attribute(.backgroundColor, at: index, effectiveRange: nil) as? NSColor {
            let components = colorComponents(background)
            if components.alpha > 0.2 {
                return components.luminance
            }
        }

        if let paragraph = storage.attribute(.paragraphStyle, at: index, effectiveRange: nil) as? NSParagraphStyle,
           !paragraph.textBlocks.isEmpty {
            return 0.95
        }

        return colorComponents(editorBackground).luminance
    }

    private static func contrastRatio(foreground: CGFloat, background: CGFloat) -> CGFloat {
        let lighter = max(foreground, background) + 0.05
        let darker = min(foreground, background) + 0.05
        return lighter / darker
    }

    private static func shouldNormalizeForeground(
        components: ColorComponents,
        backgroundLuminance: CGFloat,
        theme _: EditorTheme
    ) -> Bool {
        if components.alpha <= 0.06 {
            return true
        }

        let isNearBlack = components.luminance < 0.18
        let isNearWhite = components.luminance > 0.85
        let isNeutral = components.saturation <= 0.18
            || (isNearBlack && components.saturation < 0.5)
            || (isNearWhite && components.saturation < 0.5)
        guard isNeutral else { return false }

        return contrastRatio(foreground: components.luminance, background: backgroundLuminance) < minimumReadableContrast
    }

    private static func shouldBrightenAccent(_ components: ColorComponents, backgroundLuminance: CGFloat) -> Bool {
        guard components.alpha > 0.06, backgroundLuminance < 0.5 else { return false }
        guard components.saturation > 0.18 else { return false }
        return contrastRatio(foreground: components.luminance, background: backgroundLuminance) < minimumReadableContrast
    }

    private static func brightenForDarkTheme(_ color: NSColor) -> NSColor {
        var components = colorComponents(color)
        let targetBackground: CGFloat = 0.15
        for _ in 0..<8 {
            if contrastRatio(foreground: components.luminance, background: targetBackground) >= 3.5 {
                break
            }
            let r = min(1, components.red + (1 - components.red) * 0.28)
            let g = min(1, components.green + (1 - components.green) * 0.28)
            let b = min(1, components.blue + (1 - components.blue) * 0.28)
            components = colorComponents(NSColor(calibratedRed: r, green: g, blue: b, alpha: components.alpha))
        }
        return NSColor(
            calibratedRed: components.red,
            green: components.green,
            blue: components.blue,
            alpha: components.alpha
        )
    }

    private static func preferredTextColor(forBackgroundLuminance backgroundLuminance: CGFloat, theme: EditorTheme) -> NSColor {
        if backgroundLuminance > 0.6 {
            return EditorTheme.light.text
        }
        return theme.text
    }

    private static func shouldRemapHighlight(_ color: NSColor, for theme: EditorTheme) -> Bool {
        guard let rgb = color.usingColorSpace(.sRGB) else { return false }
        var r: CGFloat = 0
        var g: CGFloat = 0
        var b: CGFloat = 0
        var alpha: CGFloat = 0
        rgb.getRed(&r, green: &g, blue: &b, alpha: &alpha)
        guard alpha > 0.2 else { return false }

        let maxChannel = max(r, g, b)
        let minChannel = min(r, g, b)
        let saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel
        let luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b

        // Preserve neutral table and document backgrounds (common in email receipts).
        if saturation < 0.12 {
            return false
        }

        if theme.isDark {
            return luminance < 0.22
        }
        return luminance > 0.92
    }

    private static func shouldRemapDocumentBackground(_ color: NSColor, for theme: EditorTheme) -> Bool {
        guard theme.isDark else { return false }
        guard let rgb = color.usingColorSpace(.sRGB) else { return false }
        var r: CGFloat = 0
        var g: CGFloat = 0
        var b: CGFloat = 0
        var alpha: CGFloat = 0
        rgb.getRed(&r, green: &g, blue: &b, alpha: &alpha)
        guard alpha > 0.2 else { return false }

        let maxChannel = max(r, g, b)
        let minChannel = min(r, g, b)
        let saturation = maxChannel == 0 ? 0 : (maxChannel - minChannel) / maxChannel
        let luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b
        return saturation < 0.12 && luminance > 0.55
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

        let previousBackground = textView.backgroundColor
        let previousAppearance = textView.appearance
        clearDisplayColors(in: textView)
        textView.backgroundColor = .white
        textView.appearance = NSAppearance(named: .aqua)
        defer {
            textView.backgroundColor = previousBackground
            textView.appearance = previousAppearance
            applyDisplayColors(theme: EditorPreferences.shared.effectiveTheme, to: textView)
        }

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
