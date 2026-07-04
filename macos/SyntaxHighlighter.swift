import AppKit
import Foundation

enum SyntaxLanguage: String, CaseIterable {
    case plain
    case swift
    case python
    case javascript
    case html
    case css
    case json
    case markdown
    case shell
    case c

    static func detect(from url: URL?) -> SyntaxLanguage {
        guard let ext = url?.pathExtension.lowercased() else { return .plain }
        switch ext {
        case "swift": return .swift
        case "py", "pyw": return .python
        case "js", "jsx", "ts", "tsx", "mjs": return .javascript
        case "html", "htm": return .html
        case "css", "scss": return .css
        case "json": return .json
        case "md", "markdown": return .markdown
        case "sh", "bash", "zsh": return .shell
        case "c", "h", "cpp", "hpp", "m", "mm": return .c
        default: return .plain
        }
    }

    var displayName: String {
        switch self {
        case .plain: return "Plain Text"
        default: return rawValue.capitalized
        }
    }
}

struct SyntaxColors {
    let keyword: NSColor
    let string: NSColor
    let comment: NSColor
    let number: NSColor
    let type: NSColor
    let function: NSColor
    let markup: NSColor

    static func forTheme(_ theme: EditorTheme) -> SyntaxColors {
        switch theme {
        case .system:
            return forTheme(EditorTheme.systemResolved)
        case .light:
            return SyntaxColors(
                keyword: NSColor(red: 0.62, green: 0.05, blue: 0.32, alpha: 1),
                string: NSColor(red: 0.72, green: 0.08, blue: 0.06, alpha: 1),
                comment: NSColor(red: 0.30, green: 0.36, blue: 0.44, alpha: 1),
                number: NSColor(red: 0.06, green: 0.32, blue: 0.62, alpha: 1),
                type: NSColor(red: 0.12, green: 0.38, blue: 0.58, alpha: 1),
                function: NSColor(red: 0.30, green: 0.10, blue: 0.58, alpha: 1),
                markup: NSColor(red: 0.48, green: 0.24, blue: 0.06, alpha: 1)
            )
        case .dark:
            return SyntaxColors(
                keyword: NSColor(red: 1.0, green: 0.55, blue: 0.78, alpha: 1),
                string: NSColor(red: 1.0, green: 0.72, blue: 0.58, alpha: 1),
                comment: NSColor(red: 0.80, green: 0.84, blue: 0.88, alpha: 1),
                number: NSColor(red: 0.72, green: 0.90, blue: 1.0, alpha: 1),
                type: NSColor(red: 0.62, green: 0.88, blue: 1.0, alpha: 1),
                function: NSColor(red: 0.86, green: 0.72, blue: 1.0, alpha: 1),
                markup: NSColor(red: 1.0, green: 0.80, blue: 0.62, alpha: 1)
            )
        case .solarized:
            return SyntaxColors(
                keyword: NSColor(red: 0.45, green: 0.78, blue: 1.0, alpha: 1),
                string: NSColor(red: 0.78, green: 0.86, blue: 0.30, alpha: 1),
                comment: NSColor(red: 0.72, green: 0.76, blue: 0.74, alpha: 1),
                number: NSColor(red: 1.0, green: 0.42, blue: 0.38, alpha: 1),
                type: NSColor(red: 0.45, green: 0.78, blue: 1.0, alpha: 1),
                function: NSColor(red: 0.62, green: 0.70, blue: 0.92, alpha: 1),
                markup: NSColor(red: 1.0, green: 0.76, blue: 0.22, alpha: 1)
            )
        case .sepia:
            return SyntaxColors(
                keyword: NSColor(red: 0.58, green: 0.16, blue: 0.12, alpha: 1),
                string: NSColor(red: 0.24, green: 0.36, blue: 0.12, alpha: 1),
                comment: NSColor(red: 0.42, green: 0.36, blue: 0.28, alpha: 1),
                number: NSColor(red: 0.58, green: 0.26, blue: 0.08, alpha: 1),
                type: NSColor(red: 0.12, green: 0.32, blue: 0.38, alpha: 1),
                function: NSColor(red: 0.36, green: 0.20, blue: 0.42, alpha: 1),
                markup: NSColor(red: 0.52, green: 0.30, blue: 0.08, alpha: 1)
            )
        }
    }
}

private struct SyntaxPatternSet {
    let code: [(String, NSColor)]
    let literals: [(String, NSColor)]
}

final class SyntaxHighlighter {
    private let storage: NSTextStorage
    private weak var textView: NSTextView?
    private var language: SyntaxLanguage = .plain
    private var theme: EditorTheme = .light
    private var highlightWorkItem: DispatchWorkItem?
    private var scrollWorkItem: DispatchWorkItem?
    private var chunkWorkItem: DispatchWorkItem?
    private let highlightQueue = DispatchQueue(label: "com.textpad.syntax", qos: .userInitiated)
    private var contentGeneration = 0
    private var highlightRequestID = 0
    private var isApplyingHighlight = false

    private static let chunkCharacterCount = 40_000
    private static let linePadding = 3

    init(storage: NSTextStorage) {
        self.storage = storage
    }

    func attach(textView: NSTextView) {
        self.textView = textView
    }

    func configure(language: SyntaxLanguage, theme: EditorTheme) {
        self.language = language
        self.theme = theme
        highlightWorkItem?.cancel()
        scrollWorkItem?.cancel()
        chunkWorkItem?.cancel()
        contentGeneration += 1
        highlightRequestID += 1

        guard language != .plain else { return }
        guard storage.length <= LargeFileSupport.largeDocumentThreshold else { return }

        let length = storage.length
        if length > 50_000 {
            if let visible = visibleCharacterRange() {
                scheduleHighlight(in: visible)
            }
            scheduleChunkedFullHighlight(startingAt: 0)
        } else {
            scheduleHighlight(in: NSRange(location: 0, length: length))
        }
    }

    func textDidChange(editedRange: NSRange, delta: Int) {
        guard !isApplyingHighlight else { return }
        guard language != .plain else { return }
        guard storage.length <= LargeFileSupport.largeDocumentThreshold else { return }
        let expanded = expandedHighlightRange(for: editedRange, delta: delta)
        scheduleHighlight(in: expanded)
    }

    func visibleRangeDidChange() {
        guard language != .plain else { return }
        scrollWorkItem?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self, let visible = self.visibleCharacterRange() else { return }
            self.scheduleHighlight(in: visible)
        }
        scrollWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1, execute: work)
    }

    private func scheduleHighlight(in range: NSRange) {
        highlightWorkItem?.cancel()
        highlightRequestID += 1
        let requestID = highlightRequestID

        let work = DispatchWorkItem { [weak self] in
            guard let self, requestID == self.highlightRequestID else { return }
            self.applyHighlighting(in: range, requestID: requestID, contentGeneration: self.contentGeneration)
        }
        highlightWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15, execute: work)
    }

    private func scheduleChunkedFullHighlight(startingAt offset: Int) {
        chunkWorkItem?.cancel()
        let generation = contentGeneration
        let length = storage.length
        guard offset < length else { return }

        let chunkEnd = min(offset + Self.chunkCharacterCount, length)
        let chunkRange = expandedLineRange(
            around: NSRange(location: offset, length: chunkEnd - offset),
            paddingLines: Self.linePadding
        )

        let work = DispatchWorkItem { [weak self] in
            guard let self, generation == self.contentGeneration else { return }
            self.applyHighlighting(in: chunkRange, requestID: nil, contentGeneration: generation)

            guard chunkEnd < length, generation == self.contentGeneration else { return }
            let next = DispatchWorkItem { [weak self] in
                self?.scheduleChunkedFullHighlight(startingAt: chunkEnd)
            }
            self.chunkWorkItem = next
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.05, execute: next)
        }
        chunkWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2, execute: work)
    }

    private func applyHighlighting(
        in targetRange: NSRange,
        requestID: Int?,
        contentGeneration: Int
    ) {
        guard language != .plain, contentGeneration == self.contentGeneration else { return }
        if let requestID, requestID != highlightRequestID { return }

        let text = storage.string
        let nsText = text as NSString
        let fullLength = nsText.length
        guard fullLength > 0 else { return }

        let clamped = clampRange(targetRange, to: fullLength)
        guard clamped.length > 0 else { return }

        let patterns = patternsForLanguage(language)
        let baseFont = EditorPreferences.shared.font
        let baseColor = theme.text

        highlightQueue.async { [weak self] in
            guard let self else { return }
            guard contentGeneration == self.contentGeneration else { return }
            if let requestID, requestID != self.highlightRequestID { return }

            let codeMatches = self.collectMatches(
                in: text,
                fullRange: NSRange(location: 0, length: fullLength),
                patterns: patterns.code
            )
            let literalMatches = self.collectMatches(
                in: text,
                fullRange: NSRange(location: 0, length: fullLength),
                patterns: patterns.literals
            )

            DispatchQueue.main.async { [weak self] in
                guard let self else { return }
                guard contentGeneration == self.contentGeneration else { return }
                if let requestID, requestID != self.highlightRequestID { return }

                self.isApplyingHighlight = true
                self.storage.beginEditing()
                self.storage.addAttributes([
                    .font: baseFont,
                    .foregroundColor: baseColor
                ], range: clamped)

                for (range, color) in codeMatches where self.rangesIntersect(range, clamped) {
                    self.storage.addAttribute(.foregroundColor, value: color, range: range)
                }
                for (range, color) in literalMatches where self.rangesIntersect(range, clamped) {
                    self.storage.addAttribute(.foregroundColor, value: color, range: range)
                }
                self.storage.endEditing()
                self.isApplyingHighlight = false
            }
        }
    }

    private func collectMatches(
        in text: String,
        fullRange: NSRange,
        patterns: [(String, NSColor)]
    ) -> [(NSRange, NSColor)] {
        var results: [(NSRange, NSColor)] = []
        for (pattern, color) in patterns {
            guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else { continue }
            regex.enumerateMatches(in: text, range: fullRange) { match, _, _ in
                guard let range = match?.range else { return }
                results.append((range, color))
            }
        }
        return results
    }

    private func visibleCharacterRange() -> NSRange? {
        guard let textView,
              let layoutManager = textView.layoutManager,
              let textContainer = textView.textContainer else { return nil }

        layoutManager.ensureLayout(for: textContainer)
        let visibleRect = textView.visibleRect
        let glyphRange = layoutManager.glyphRange(forBoundingRect: visibleRect, in: textContainer)
        let charRange = layoutManager.characterRange(forGlyphRange: glyphRange, actualGlyphRange: nil)
        return expandedLineRange(around: charRange, paddingLines: Self.linePadding)
    }

    private func expandedHighlightRange(for editedRange: NSRange, delta: Int) -> NSRange {
        let nsText = storage.string as NSString
        let length = nsText.length
        guard length > 0 else { return NSRange(location: 0, length: 0) }

        let safeEdited = clampRange(editedRange, to: length)
        var range = nsText.lineRange(for: safeEdited)
        range = expandedLineRange(around: range, paddingLines: Self.linePadding)
        range = expandForUnclosedLiterals(startingAt: range.location, in: nsText)
        return clampRange(range, to: length)
    }

    private func expandedLineRange(around range: NSRange, paddingLines: Int) -> NSRange {
        let nsText = storage.string as NSString
        let length = nsText.length
        guard length > 0 else { return NSRange(location: 0, length: 0) }

        var result = clampRange(range, to: length)
        var lineStart = nsText.lineRange(for: NSRange(location: result.location, length: 0)).location

        for _ in 0..<paddingLines where lineStart > 0 {
            let previous = max(0, lineStart - 1)
            lineStart = nsText.lineRange(for: NSRange(location: previous, length: 0)).location
        }

        var lineEnd = NSMaxRange(nsText.lineRange(for: NSRange(location: min(NSMaxRange(result), length - 1), length: 0)))
        for _ in 0..<paddingLines where lineEnd < length {
            let next = min(length - 1, lineEnd)
            lineEnd = NSMaxRange(nsText.lineRange(for: NSRange(location: next, length: 0)))
        }

        result.location = lineStart
        result.length = lineEnd - lineStart
        return clampRange(result, to: length)
    }

    private func expandForUnclosedLiterals(startingAt location: Int, in text: NSString) -> NSRange {
        let scanStart = max(0, location - 10_000)
        let prefix = text.substring(with: NSRange(location: scanStart, length: location - scanStart))
        var start = location

        if let blockStart = prefix.range(of: "/*", options: .backwards) {
            let tail = prefix[blockStart.upperBound...]
            if tail.range(of: "*/") == nil {
                start = scanStart + prefix.distance(from: prefix.startIndex, to: blockStart.lowerBound)
            }
        }

        for opener in ["\"\"\"", "'''", "\"", "'"] {
            if let stringStart = prefix.range(of: opener, options: .backwards) {
                let index = prefix.distance(from: prefix.startIndex, to: stringStart.lowerBound)
                let candidate = scanStart + index
                let tail = prefix[stringStart.lowerBound...]
                if !isClosedLiteral(opener: opener, in: tail) {
                    start = min(start, candidate)
                }
            }
        }

        let length = text.length
        return NSRange(location: start, length: max(0, length - start))
    }

    private func isClosedLiteral(opener: String, in tail: Substring) -> Bool {
        var searchStart = tail.index(tail.startIndex, offsetBy: opener.count)
        while searchStart < tail.endIndex,
              let found = tail.range(of: opener, range: searchStart..<tail.endIndex) {
            if found.lowerBound == tail.startIndex || tail[tail.index(before: found.lowerBound)] != "\\" {
                return true
            }
            searchStart = found.upperBound
        }
        return false
    }

    private func clampRange(_ range: NSRange, to length: Int) -> NSRange {
        guard length > 0 else { return NSRange(location: 0, length: 0) }
        let location = max(0, min(range.location, length))
        let end = max(location, min(range.location + range.length, length))
        return NSRange(location: location, length: end - location)
    }

    private func rangesIntersect(_ lhs: NSRange, _ rhs: NSRange) -> Bool {
        NSIntersectionRange(lhs, rhs).length > 0
    }

    private func patternsForLanguage(_ language: SyntaxLanguage) -> SyntaxPatternSet {
        let c = SyntaxColors.forTheme(theme)
        let hashComment = #"(?m)#.*$"#
        let cStyleComment = #"(?m)//.*$|/\*[\s\S]*?\*/"#
        let string = #""(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'"#
        let jsonString = #""(?:\\.|[^"\\])*"(?!\s*:)"#
        let number = #"\b\d+(?:\.\d+)?\b"#

        switch language {
        case .plain:
            return SyntaxPatternSet(code: [], literals: [])
        case .swift:
            let kw = #"\b(?:import|class|struct|enum|protocol|extension|func|var|let|if|else|guard|switch|case|default|for|while|return|try|catch|throw|async|await|private|public|internal|static|self|super|init|deinit|true|false|nil|in|where|as|is|break|continue|defer|do|repeat|typealias|associatedtype|some|any)\b"#
            let type = #"\b(?:String|Int|Double|Float|Bool|Void|Array|Dictionary|Set|Optional|Any|AnyObject|CGFloat|NSObject|URL|Data|Date)\b"#
            let directive = #"(?m)^\s*#\s*\w+.*$"#
            return SyntaxPatternSet(
                code: [(directive, c.markup), (kw, c.keyword), (type, c.type), (number, c.number)],
                literals: [(cStyleComment, c.comment), (string, c.string)]
            )
        case .python:
            let kw = #"\b(?:def|class|import|from|if|elif|else|for|while|return|try|except|finally|with|as|pass|break|continue|lambda|yield|global|nonlocal|True|False|None|and|or|not|in|is|async|await|raise|del)\b"#
            return SyntaxPatternSet(
                code: [(kw, c.keyword), (number, c.number)],
                literals: [
                    (hashComment, c.comment),
                    (#"'''[\s\S]*?'''|\"\"\"[\s\S]*?\"\"\""#, c.string),
                    (string, c.string)
                ]
            )
        case .javascript:
            let kw = #"\b(?:const|let|var|function|class|extends|import|export|default|if|else|for|while|return|try|catch|finally|throw|new|this|super|async|await|typeof|instanceof|true|false|null|undefined|switch|case|break|continue|do|of|in)\b"#
            return SyntaxPatternSet(
                code: [(kw, c.keyword), (number, c.number)],
                literals: [(cStyleComment, c.comment), (string, c.string)]
            )
        case .html:
            let tag = #"</?[a-zA-Z][^>]*>"#
            let attr = #"\b(?:href|src|class|id|style|type|name|value|content|rel|alt|title)\b"#
            let htmlComment = #"<!--[\s\S]*?-->"#
            return SyntaxPatternSet(
                code: [(tag, c.markup), (attr, c.keyword)],
                literals: [(htmlComment, c.comment), (string, c.string)]
            )
        case .css:
            let prop = #"(?<=\{|;)\s*[a-zA-Z-]+\s*:"#
            let selector = #"(?m)^[^{]+(?=\{)"#
            return SyntaxPatternSet(
                code: [(selector, c.type), (prop, c.keyword), (#"#[0-9a-fA-F]{3,8}\b"#, c.number)],
                literals: [(cStyleComment, c.comment), (string, c.string)]
            )
        case .json:
            let key = #"(?m)^\s*"[^"]+"\s*(?=:)"#
            let literal = #"\b(?:true|false|null)\b"#
            return SyntaxPatternSet(
                code: [(key, c.type), (literal, c.keyword), (number, c.number)],
                literals: [(jsonString, c.string)]
            )
        case .markdown:
            let heading = #"(?m)^#{1,6}\s+.+$"#
            let code = #"`[^`]+`"#
            let link = #"\[[^\]]+\]\([^)]+\)"#
            return SyntaxPatternSet(
                code: [(heading, c.keyword), (link, c.type), (#"(?m)^>\s+.+$"#, c.comment)],
                literals: [(code, c.string)]
            )
        case .shell:
            let kw = #"\b(?:if|then|else|elif|fi|for|do|done|while|case|esac|function|return|exit|export|source|local|readonly)\b"#
            return SyntaxPatternSet(
                code: [(kw, c.keyword), (#"\$\{?[a-zA-Z_][a-zA-Z0-9_]*\}?"#, c.type)],
                literals: [(hashComment, c.comment), (string, c.string)]
            )
        case .c:
            let kw = #"\b(?:if|else|for|while|do|switch|case|default|break|continue|return|struct|union|enum|typedef|static|extern|const|volatile|void|int|char|float|double|long|short|unsigned|signed|sizeof|include|define|ifdef|ifndef|endif|pragma|true|false|NULL|class|public|private|protected|virtual|override|namespace|using|template|new|delete)\b"#
            let preproc = #"(?m)^\s*#\s*\w+.*$"#
            return SyntaxPatternSet(
                code: [(preproc, c.markup), (kw, c.keyword), (number, c.number)],
                literals: [(cStyleComment, c.comment), (string, c.string)]
            )
        }
    }
}