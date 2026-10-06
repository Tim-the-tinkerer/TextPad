import AppKit

/// Writes rich text and plain text as Markdown.
enum MarkdownExport {
    static func markdown(from attributed: NSAttributedString) -> String {
        let source = attributed.string as NSString
        if source.length == 0 { return "" }

        var blocks: [Block] = []
        var numbers: [Int: Int] = [:]
        var table: TableBuilder?
        var index = 0
        while index < source.length {
            let paragraph = source.paragraphRange(for: NSRange(location: index, length: 0))
            let content = rangeWithoutTerminator(paragraph, in: source)
            let style = attributed.attribute(.paragraphStyle, at: paragraph.location, effectiveRange: nil) as? NSParagraphStyle
            let cell = style?.textBlocks.first as? NSTextTableBlock

            if let cell {
                if table?.table !== cell.table {
                    flush(&table, into: &blocks)
                    table = TableBuilder(table: cell.table)
                }
                let text = inlineMarkdown(attributed, range: content, inTable: true)
                    .replacingOccurrences(of: "\n", with: " ")
                table?.set(row: cell.startingRow, column: cell.startingColumn, text: text)
                numbers.removeAll()
            } else {
                flush(&table, into: &blocks)
                let text = inlineMarkdown(attributed, range: content, inTable: false)
                if let list = style?.textLists.last {
                    let level = listLevel(style)
                    let indent = String(repeating: "  ", count: level)
                    let marker: String
                    if isOrdered(list.markerFormat) {
                        let next = numbers[level] ?? max(list.startingItemNumber, 1)
                        numbers[level] = next + 1
                        for key in numbers.keys where key > level {
                            numbers[key] = nil
                        }
                        marker = "\(next). "
                    } else {
                        numbers.removeAll()
                        marker = "- "
                    }
                    blocks.append(Block(text: indent + marker + text, isList: true, ordered: isOrdered(list.markerFormat), level: level))
                } else {
                    numbers.removeAll()
                    if !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                        blocks.append(Block(text: text, isList: false))
                    } else if !blocks.isEmpty {
                        blocks.append(Block(text: "", isList: false))
                    }
                }
            }

            let next = NSMaxRange(paragraph)
            if next <= index { break }
            index = next
        }
        flush(&table, into: &blocks)
        return join(blocks)
    }

    private struct Block {
        var text: String
        var isList: Bool
        var ordered = false
        var level = 0
    }

    private final class TableBuilder {
        let table: NSTextTable
        private var rows: [[String]] = []
        private var columns = 0

        init(table: NSTextTable) {
            self.table = table
        }

        func set(row: Int, column: Int, text: String) {
            guard row >= 0, column >= 0 else { return }
            while rows.count <= row {
                rows.append([])
            }
            while rows[row].count <= column {
                rows[row].append("")
            }
            let existing = rows[row][column]
            rows[row][column] = existing.isEmpty ? text : existing + " " + text
            columns = max(columns, column + 1)
        }

        func markdown() -> String {
            let width = max(columns, 1)
            func line(_ cells: [String]) -> String {
                var padded = cells
                while padded.count < width { padded.append("") }
                return "| " + padded.joined(separator: " | ") + " |"
            }
            guard let header = rows.first else { return "" }
            var lines = [line(header), "| " + Array(repeating: "---", count: width).joined(separator: " | ") + " |"]
            for row in rows.dropFirst() {
                lines.append(line(row))
            }
            return lines.joined(separator: "\n")
        }
    }

    private static func flush(_ table: inout TableBuilder?, into blocks: inout [Block]) {
        guard let current = table else { return }
        let text = current.markdown()
        if !text.isEmpty {
            blocks.append(Block(text: text, isList: false))
        }
        table = nil
    }

    private static func join(_ blocks: [Block]) -> String {
        var output = ""
        var previous: Block?
        for block in blocks {
            if !block.isList && block.text.isEmpty {
                previous = block
                continue
            }
            if output.isEmpty {
                output = block.text
            } else if continuesList(block, after: previous) {
                output += "\n" + block.text
            } else {
                output += "\n\n" + block.text
            }
            previous = block
        }
        if output.isEmpty { return "" }
        if !output.hasSuffix("\n") { output += "\n" }
        return output
    }

    /// Same-list items stay on consecutive lines, including a nested item under its parent.
    /// A bullet list followed by a numbered list is a new list, so it gets a blank line.
    private static func continuesList(_ block: Block, after previous: Block?) -> Bool {
        guard let previous, block.isList, previous.isList else { return false }
        if block.level > previous.level { return true }
        return block.ordered == previous.ordered
    }

    private static func listLevel(_ style: NSParagraphStyle?) -> Int {
        guard let style else { return 0 }
        let fromLists = max(0, style.textLists.count - 1)
        let fromIndent = max(0, Int((style.headIndent / 24).rounded(.down)) - (style.textLists.isEmpty ? 0 : 1))
        return max(fromLists, fromIndent)
    }

    private static func isOrdered(_ format: NSTextList.MarkerFormat) -> Bool {
        switch format {
        case .decimal, .lowercaseAlpha, .uppercaseAlpha, .lowercaseRoman, .uppercaseRoman,
             .lowercaseLatin, .uppercaseLatin:
            return true
        default:
            return false
        }
    }

    private static func rangeWithoutTerminator(_ range: NSRange, in source: NSString) -> NSRange {
        var content = range
        while content.length > 0 {
            let character = source.character(at: NSMaxRange(content) - 1)
            if character == 10 || character == 13 {
                content.length -= 1
            } else {
                break
            }
        }
        return content
    }

    private struct Piece {
        var text: String
        var bold: Bool
        var italic: Bool
        var strike: Bool
        var underline: Bool
        var link: String?
        var hardBreak: Bool
    }

    private static func inlineMarkdown(_ attributed: NSAttributedString, range: NSRange, inTable: Bool) -> String {
        if range.length == 0 { return "" }
        var pieces: [Piece] = []
        attributed.enumerateAttributes(in: range, options: []) { attributes, subrange, _ in
            let raw = (attributed.string as NSString).substring(with: subrange)
            let parts = raw.components(separatedBy: "\u{2028}")
            for (index, part) in parts.enumerated() {
                if index > 0 {
                    pieces.append(Piece(text: "", bold: false, italic: false, strike: false, underline: false, link: nil, hardBreak: true))
                }
                let font = attributes[.font] as? NSFont
                let traits = font?.fontDescriptor.symbolicTraits ?? []
                var bold = traits.contains(.bold)
                let italic = traits.contains(.italic)
                if let traitMap = font?.fontDescriptor.object(forKey: .traits) as? [NSFontDescriptor.TraitKey: Any],
                   let weight = traitMap[.weight] as? NSNumber,
                   weight.doubleValue >= NSFont.Weight.bold.rawValue {
                    bold = true
                }
                let strike = (attributes[.strikethroughStyle] as? Int ?? 0) != 0
                let underline = (attributes[.underlineStyle] as? Int ?? 0) != 0
                let link = linkString(attributes[.link])
                pieces.append(Piece(text: part, bold: bold, italic: italic, strike: strike, underline: underline, link: link, hardBreak: false))
            }
        }
        return render(merge(pieces), inTable: inTable)
    }

    private static func linkString(_ value: Any?) -> String? {
        switch value {
        case let url as URL:
            return url.absoluteString
        case let url as NSURL:
            return url.absoluteString
        case let text as String where !text.isEmpty:
            return text
        default:
            return nil
        }
    }

    private static func merge(_ pieces: [Piece]) -> [Piece] {
        var merged: [Piece] = []
        for piece in pieces {
            if piece.hardBreak {
                merged.append(piece)
                continue
            }
            if var last = merged.last, !last.hardBreak,
               last.bold == piece.bold, last.italic == piece.italic, last.strike == piece.strike,
               last.underline == piece.underline, last.link == piece.link {
                last.text += piece.text
                merged[merged.count - 1] = last
            } else {
                merged.append(piece)
            }
        }
        return merged
    }

    private static func render(_ pieces: [Piece], inTable: Bool) -> String {
        var output = ""
        var atLineStart = true
        for piece in pieces {
            if piece.hardBreak {
                output += inTable ? " " : "  \n"
                atLineStart = true
                continue
            }
            output += wrap(piece, atLineStart: atLineStart && !inTable, inTable: inTable)
            atLineStart = false
        }
        return output
    }

    private static func wrap(_ piece: Piece, atLineStart: Bool, inTable: Bool) -> String {
        if piece.text.isEmpty { return "" }
        if piece.text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return piece.text
        }
        var text = escape(piece.text, atLineStart: atLineStart, inTable: inTable)
        if piece.bold && piece.italic {
            text = "***\(text)***"
        } else if piece.bold {
            text = "**\(text)**"
        } else if piece.italic {
            text = "*\(text)*"
        }
        if piece.strike {
            text = "~~\(text)~~"
        }
        if piece.underline {
            text = "<u>\(text)</u>"
        }
        if let link = piece.link, !link.isEmpty {
            text = "[\(text)](\(linkDestination(link)))"
        }
        return text
    }

    private static func linkDestination(_ link: String) -> String {
        if link.contains(" ") || link.contains("(") || link.contains(")") {
            return "<\(link.replacingOccurrences(of: ">", with: "%3E"))>"
        }
        return link.replacingOccurrences(of: " ", with: "%20")
    }

    /// Writes plain text as Markdown that shows the same words and line breaks.
    /// A document whose syntax language is already Markdown is written by the export command as typed.
    static func markdown(fromPlainText text: String) -> String {
        let normalized = text
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        if normalized.isEmpty { return "" }

        var lines = normalized.split(separator: "\n", omittingEmptySubsequences: false).map(String.init)
        if lines.last?.isEmpty == true {
            lines.removeLast()
        }

        var paragraphs: [[String]] = []
        var current: [String] = []
        func flush() {
            if !current.isEmpty {
                paragraphs.append(current)
                current = []
            }
        }
        for line in lines {
            if isBlankLine(line) {
                flush()
            } else {
                current.append(convertPlainLine(line))
            }
        }
        flush()
        if paragraphs.isEmpty { return "" }

        let body = paragraphs.map { paragraph in
            paragraph.enumerated().map { index, line in
                index < paragraph.count - 1 ? line + "  " : line
            }.joined(separator: "\n")
        }.joined(separator: "\n\n")
        return body + "\n"
    }

    /// The converter emits LF. Under Preserve, put the source document's line ending back
    /// before the save policy runs. LF and CRLF policies still replace it afterwards.
    static func applyingSourceLineEndings(_ markdown: String, source: String) -> String {
        switch LineEnding.detect(in: source) {
        case .crlf:
            return markdown.replacingOccurrences(of: "\n", with: "\r\n")
        case .cr:
            return markdown.replacingOccurrences(of: "\n", with: "\r")
        case .lf, .mixed:
            return markdown
        }
    }

    private static func isBlankLine(_ line: String) -> Bool {
        line.allSatisfy { $0 == " " || $0 == "\t" }
    }

    private static func convertPlainLine(_ line: String) -> String {
        var index = line.startIndex
        var indent = ""
        while index < line.endIndex, line[index] == " " || line[index] == "\t" {
            indent += String(repeating: "\u{00A0}", count: line[index] == "\t" ? 4 : 1)
            index = line.index(after: index)
        }
        let rest = trimTrailingWhitespace(String(line[index...]))
        let escaped = escape(rest, atLineStart: true, inTable: false, escapePipes: true)
        return indent + escaped.replacingOccurrences(of: "\t", with: String(repeating: "\u{00A0}", count: 4))
    }

    private static func trimTrailingWhitespace(_ text: String) -> String {
        var end = text.endIndex
        while end > text.startIndex {
            let previous = text.index(before: end)
            if text[previous] == " " || text[previous] == "\t" {
                end = previous
            } else {
                break
            }
        }
        return String(text[..<end])
    }

    private static func escape(_ text: String, atLineStart: Bool, inTable: Bool, escapePipes: Bool = false) -> String {
        let body = text
        var prefix = ""
        if atLineStart {
            if body.range(of: #"^\d+[.)]\s"#, options: .regularExpression) != nil {
                prefix = "\\"
            } else if let first = body.first, "#>+-=".contains(first) {
                prefix = "\\"
            }
        }
        var escaped = ""
        for character in body {
            switch character {
            case "\\", "`", "*", "_", "[", "]", "~", "<":
                escaped.append("\\")
                escaped.append(character)
            case "|" where inTable || escapePipes:
                escaped.append("\\")
                escaped.append(character)
            default:
                escaped.append(character)
            }
        }
        return prefix + escaped
    }
}
