import Foundation

/// A practical Markdown subset shared by preview and HTML export.
/// Headings, paragraphs, lists, quotes, fences, tables, emphasis, links, and images.
/// Raw HTML in the source is escaped. Unsafe link schemes are dropped.
enum Markdown {
    struct PageStyle {
        var background: String
        var foreground: String
        var muted: String
        var codeBackground: String
        var accent: String
        var border: String

        static let export = PageStyle(
            background: "#ffffff",
            foreground: "#1a1a1f",
            muted: "#5c6570",
            codeBackground: "#f3f4f6",
            accent: "#0b57d0",
            border: "#e4e4ea"
        )
    }

    static func htmlDocument(from markdown: String, title: String, style: PageStyle = .export) -> String {
        let safeTitle = escape(title)
        let body = htmlBody(from: markdown)
        return """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>\(safeTitle)</title>
        <style>
        body { background:\(style.background); color:\(style.foreground); font:16px/1.55 -apple-system,BlinkMacSystemFont,"Segoe UI",Helvetica,Arial,sans-serif; margin:0; padding:2rem 1.25rem 3rem; }
        main { max-width:42rem; margin:0 auto; }
        h1,h2,h3,h4,h5,h6 { line-height:1.25; margin:1.4em 0 0.4em; }
        p,ul,ol,blockquote,pre,table { margin:0.8em 0; }
        a { color:\(style.accent); }
        code,pre { font-family:ui-monospace,Menlo,Consolas,monospace; font-size:0.92em; }
        code { background:\(style.codeBackground); padding:0.1em 0.35em; border-radius:4px; }
        pre { background:\(style.codeBackground); padding:0.85em 1em; overflow:auto; border-radius:8px; }
        pre code { background:none; padding:0; }
        blockquote { margin-left:0; padding-left:1em; border-left:3px solid \(style.border); color:\(style.muted); }
        hr { border:0; border-top:1px solid \(style.border); margin:1.5em 0; }
        img { max-width:100%; }
        table { border-collapse:collapse; width:100%; }
        th,td { border:1px solid \(style.border); padding:0.35em 0.6em; text-align:left; vertical-align:top; }
        ul,ol { padding-left:1.4em; }
        li { margin:0.2em 0; }
        </style>
        </head>
        <body>
        <main>
        \(body)
        </main>
        </body>
        </html>
        """
    }

    static func htmlBody(from markdown: String) -> String {
        let normalized = markdown
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        let lines = normalized.split(separator: "\n", omittingEmptySubsequences: false).map(String.init)
        return renderBlocks(lines).joined(separator: "\n")
    }

    // MARK: - Blocks

    private static func renderBlocks(_ lines: [String]) -> [String] {
        var index = 0
        var blocks: [String] = []
        while index < lines.count {
            if isBlank(lines[index]) {
                index += 1
                continue
            }
            if let fence = readFence(lines, from: &index) {
                blocks.append(fence)
                continue
            }
            if let heading = atxHeading(lines[index]) {
                blocks.append(heading)
                index += 1
                continue
            }
            if isTableStart(lines, index) {
                let table = readTable(lines, from: &index)
                blocks.append(table)
                continue
            }
            if isThematicBreak(lines[index]) {
                blocks.append("<hr>")
                index += 1
                continue
            }
            if isQuoteLine(lines[index]) {
                let quote = readQuote(lines, from: &index)
                blocks.append(quote)
                continue
            }
            if listMarker(lines[index]) != nil, indentColumns(lines[index]) < 4 {
                let list = readList(lines, from: &index, baseIndent: indentColumns(lines[index]))
                blocks.append(list)
                continue
            }
            if indentColumns(lines[index]) >= 4 {
                let code = readIndentedCode(lines, from: &index)
                blocks.append(code)
                continue
            }
            let paragraph = readParagraph(lines, from: &index)
            blocks.append(paragraph)
        }
        return blocks
    }

    private static func readFence(_ lines: [String], from index: inout Int) -> String? {
        guard let open = fenceOpen(lines[index]) else { return nil }
        let start = index + 1
        var end = start
        while end < lines.count {
            if fenceClose(lines[end], marker: open.marker, minimum: open.count) {
                break
            }
            end += 1
        }
        let body = lines[start..<min(end, lines.count)].joined(separator: "\n")
        index = min(end + 1, lines.count)
        let language = open.info.split(whereSeparator: { $0.isWhitespace }).first.map(String.init) ?? ""
        let className = language.allSatisfy { $0.isLetter || $0.isNumber || $0 == "_" || $0 == "-" || $0 == "+" }
            ? language
            : ""
        let classAttr = className.isEmpty ? "" : " class=\"language-\(escape(className))\""
        return "<pre><code\(classAttr)>\(escape(body))</code></pre>"
    }

    private static func fenceOpen(_ line: String) -> (marker: Character, count: Int, info: String)? {
        let (indent, rest) = splitIndent(line)
        guard indent < 4, let first = rest.first, first == "`" || first == "~" else { return nil }
        var count = 0
        var idx = rest.startIndex
        while idx < rest.endIndex, rest[idx] == first {
            count += 1
            idx = rest.index(after: idx)
        }
        guard count >= 3 else { return nil }
        let info = String(rest[idx...]).trimmingCharacters(in: .whitespaces)
        if first == "`", info.contains("`") { return nil }
        return (first, count, info)
    }

    private static func fenceClose(_ line: String, marker: Character, minimum: Int) -> Bool {
        let (indent, rest) = splitIndent(line)
        guard indent < 4 else { return false }
        let trimmed = rest.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty, trimmed.allSatisfy({ $0 == marker }) else { return false }
        return trimmed.count >= minimum
    }

    private static func atxHeading(_ line: String) -> String? {
        let (indent, rest) = splitIndent(line)
        guard indent < 4, rest.first == "#" else { return nil }
        var level = 0
        var idx = rest.startIndex
        while idx < rest.endIndex, rest[idx] == "#", level < 6 {
            level += 1
            idx = rest.index(after: idx)
        }
        guard level >= 1 else { return nil }
        if idx < rest.endIndex, !rest[idx].isWhitespace { return nil }
        var text = String(rest[idx...]).trimmingCharacters(in: .whitespaces)
        while text.hasSuffix("#") {
            text.removeLast()
        }
        text = text.trimmingCharacters(in: .whitespaces)
        return "<h\(level)>\(renderInline(text))</h\(level)>"
    }

    private static func isTableStart(_ lines: [String], _ index: Int) -> Bool {
        guard index + 1 < lines.count else { return false }
        guard lines[index].contains("|"), isTableSeparator(lines[index + 1]) else { return false }
        return splitRow(lines[index]).count >= 1 && splitRow(lines[index + 1]).count >= 1
    }

    private static func isTableSeparator(_ line: String) -> Bool {
        let cells = splitRow(line)
        guard !cells.isEmpty else { return false }
        return cells.allSatisfy { cell in
            let trimmed = cell.trimmingCharacters(in: .whitespaces)
            guard trimmed.contains("-") else { return false }
            return trimmed.allSatisfy { $0 == "-" || $0 == ":" || $0 == " " || $0 == "\t" }
        }
    }

    private static func readTable(_ lines: [String], from index: inout Int) -> String {
        let header = splitRow(lines[index])
        index += 2
        var rows: [[String]] = []
        while index < lines.count, !isBlank(lines[index]), lines[index].contains("|") {
            rows.append(splitRow(lines[index]))
            index += 1
        }
        var html = "<table>\n<thead><tr>"
        for cell in header {
            html += "<th>\(renderInline(cell))</th>"
        }
        html += "</tr></thead>\n<tbody>\n"
        for row in rows {
            html += "<tr>"
            for column in 0..<header.count {
                let cell = column < row.count ? row[column] : ""
                html += "<td>\(renderInline(cell))</td>"
            }
            html += "</tr>\n"
        }
        html += "</tbody></table>"
        return html
    }

    private static func splitRow(_ line: String) -> [String] {
        var text = line.trimmingCharacters(in: .whitespaces)
        if text.hasPrefix("|") { text.removeFirst() }
        if text.hasSuffix("|"), !text.hasSuffix("\\|") { text.removeLast() }
        var cells: [String] = []
        var current = ""
        var escaped = false
        for character in text {
            if escaped {
                current.append(character)
                escaped = false
                continue
            }
            if character == "\\" {
                current.append(character)
                escaped = true
                continue
            }
            if character == "|" {
                cells.append(current.trimmingCharacters(in: .whitespaces))
                current = ""
                continue
            }
            current.append(character)
        }
        cells.append(current.trimmingCharacters(in: .whitespaces))
        return cells
    }

    private static func readQuote(_ lines: [String], from index: inout Int) -> String {
        var inner: [String] = []
        while index < lines.count {
            if isBlank(lines[index]) {
                let next = index + 1
                if next < lines.count, isQuoteLine(lines[next]) {
                    inner.append("")
                    index += 1
                    continue
                }
                break
            }
            guard isQuoteLine(lines[index]) else { break }
            inner.append(stripQuote(lines[index]))
            index += 1
        }
        let body = renderBlocks(inner).joined(separator: "\n")
        return "<blockquote>\n\(body)\n</blockquote>"
    }

    private static func isQuoteLine(_ line: String) -> Bool {
        let (indent, rest) = splitIndent(line)
        return indent < 4 && rest.first == ">"
    }

    private static func stripQuote(_ line: String) -> String {
        let (_, rest) = splitIndent(line)
        var text = rest
        if text.first == ">" { text = text.dropFirst() }
        if text.first == " " || text.first == "\t" { text = text.dropFirst() }
        return String(text)
    }

    private struct ListMark {
        var indent: Int
        var ordered: Bool
        var number: Int
        var content: String
        var task: String?
    }

    private static func listMarker(_ line: String) -> ListMark? {
        let (indent, rest) = splitIndent(line)
        guard let first = rest.first else { return nil }
        if first == "-" || first == "*" || first == "+" {
            let after = rest.dropFirst()
            guard after.first == " " || after.first == "\t" else { return nil }
            var content = String(after.dropFirst())
            var task: String?
            if content.hasPrefix("[ ] ") || content.hasPrefix("[x] ") || content.hasPrefix("[X] ") {
                task = content.hasPrefix("[ ] ") ? "☐" : "☑"
                content = String(content.dropFirst(4))
            }
            return ListMark(indent: indent, ordered: false, number: 1, content: content, task: task)
        }
        var idx = rest.startIndex
        var digits = ""
        while idx < rest.endIndex, rest[idx].isNumber, digits.count < 9 {
            digits.append(rest[idx])
            idx = rest.index(after: idx)
        }
        guard !digits.isEmpty, idx < rest.endIndex, rest[idx] == "." || rest[idx] == ")" else { return nil }
        idx = rest.index(after: idx)
        guard idx < rest.endIndex, rest[idx] == " " || rest[idx] == "\t" else { return nil }
        idx = rest.index(after: idx)
        return ListMark(indent: indent, ordered: true, number: Int(digits) ?? 1, content: String(rest[idx...]), task: nil)
    }

    private static func readList(_ lines: [String], from index: inout Int, baseIndent: Int) -> String {
        guard let first = listMarker(lines[index]) else { return "" }
        let ordered = first.ordered
        var items: [String] = []
        while index < lines.count {
            if isBlank(lines[index]) { break }
            guard let mark = listMarker(lines[index]) else { break }
            if mark.indent < baseIndent { break }
            if mark.indent > baseIndent {
                let nested = readList(lines, from: &index, baseIndent: mark.indent)
                if items.isEmpty {
                    items.append(nested)
                } else {
                    var last = items.removeLast()
                    if last.hasSuffix("</li>") {
                        last.removeLast(5)
                        last += "\n" + nested + "</li>"
                    } else {
                        last += "\n" + nested
                    }
                    items.append(last)
                }
                continue
            }
            if mark.ordered != ordered { break }
            var content = renderInline(mark.content)
            if let task = mark.task {
                content = "\(task) \(content)"
            }
            index += 1
            while index < lines.count, !isBlank(lines[index]), listMarker(lines[index]) == nil {
                let indent = indentColumns(lines[index])
                if indent < baseIndent + 2 { break }
                content += " " + renderInline(lines[index].trimmingCharacters(in: .whitespaces))
                index += 1
            }
            items.append("<li>\(content)</li>")
        }
        let tag = ordered ? "ol" : "ul"
        let start = ordered && first.number != 1 ? " start=\"\(first.number)\"" : ""
        return "<\(tag)\(start)>\n" + items.joined(separator: "\n") + "\n</\(tag)>"
    }

    private static func readIndentedCode(_ lines: [String], from index: inout Int) -> String {
        var body: [String] = []
        while index < lines.count {
            if isBlank(lines[index]) {
                let next = index + 1
                if next < lines.count, !isBlank(lines[next]), indentColumns(lines[next]) >= 4 {
                    body.append("")
                    index += 1
                    continue
                }
                break
            }
            guard indentColumns(lines[index]) >= 4 else { break }
            body.append(stripIndent(lines[index], columns: 4))
            index += 1
        }
        return "<pre><code>\(escape(body.joined(separator: "\n")))</code></pre>"
    }

    private static func readParagraph(_ lines: [String], from index: inout Int) -> String {
        var collected: [String] = [lines[index]]
        index += 1
        while index < lines.count {
            if isBlank(lines[index]) { break }
            if isSetextUnderline(lines[index]) {
                let level = lines[index].contains("=") ? 1 : 2
                let text = collected.joined(separator: " ")
                index += 1
                return "<h\(level)>\(renderInline(text))</h\(level)>"
            }
            if fenceOpen(lines[index]) != nil
                || atxHeading(lines[index]) != nil
                || isThematicBreak(lines[index])
                || isQuoteLine(lines[index])
                || (listMarker(lines[index]) != nil && indentColumns(lines[index]) < 4)
                || isTableStart(lines, index) {
                break
            }
            collected.append(lines[index])
            index += 1
        }
        return renderParagraph(collected)
    }

    private static func renderParagraph(_ lines: [String]) -> String {
        var chunks: [String] = [""]
        for (offset, raw) in lines.enumerated() {
            var line = raw
            var hard = false
            if line.hasSuffix("\\"), !line.dropLast().hasSuffix("\\") {
                hard = true
                line.removeLast()
            }
            var spaces = 0
            while line.last == " " {
                spaces += 1
                line.removeLast()
            }
            if spaces >= 2 { hard = true }
            if !chunks[chunks.count - 1].isEmpty {
                chunks[chunks.count - 1] += " "
            }
            chunks[chunks.count - 1] += line
            if hard, offset < lines.count - 1 {
                chunks.append("")
            }
        }
        let html = chunks.map { renderInline($0) }.joined(separator: "<br>\n")
        return "<p>\(html)</p>"
    }

    private static func isSetextUnderline(_ line: String) -> Bool {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        guard trimmed.count >= 1 else { return false }
        return trimmed.allSatisfy { $0 == "=" } || trimmed.allSatisfy { $0 == "-" }
    }

    private static func isThematicBreak(_ line: String) -> Bool {
        let (indent, rest) = splitIndent(line)
        guard indent < 4 else { return false }
        let trimmed = rest.trimmingCharacters(in: .whitespaces)
        guard trimmed.count >= 3 else { return false }
        let marker = trimmed.first
        guard marker == "-" || marker == "*" || marker == "_" else { return false }
        return trimmed.allSatisfy { $0 == marker || $0 == " " || $0 == "\t" }
            && trimmed.filter({ $0 == marker }).count >= 3
    }

    // MARK: - Inlines

    private static func renderInline(_ text: String) -> String {
        var result = ""
        var index = text.startIndex
        while index < text.endIndex {
            if text[index] == "\\" {
                let next = text.index(after: index)
                if next < text.endIndex, isAsciiPunctuation(text[next]) {
                    result += escape(String(text[next]))
                    index = text.index(after: next)
                    continue
                }
            }
            if text[index] == "`", let span = codeSpan(text, at: index) {
                result += "<code>\(escape(span.code))</code>"
                index = span.end
                continue
            }
            if text[index] == "!" || text[index] == "[", let link = linkOrImage(text, at: index) {
                result += link.html
                index = link.end
                continue
            }
            if text[index] == "<", let link = autolink(text, at: index) {
                result += link.html
                index = link.end
                continue
            }
            if let wrapped = emphasis(text, at: index) {
                result += wrapped.html
                index = wrapped.end
                continue
            }
            result += escape(String(text[index]))
            index = text.index(after: index)
        }
        return result
    }

    private static func codeSpan(_ text: String, at start: String.Index) -> (code: String, end: String.Index)? {
        var count = 0
        var index = start
        while index < text.endIndex, text[index] == "`" {
            count += 1
            index = text.index(after: index)
        }
        guard count > 0 else { return nil }
        var scan = index
        while scan < text.endIndex {
            if text[scan] == "`" {
                var close = 0
                var end = scan
                while end < text.endIndex, text[end] == "`" {
                    close += 1
                    end = text.index(after: end)
                }
                if close == count {
                    var code = String(text[index..<scan])
                    code = code.replacingOccurrences(of: "\n", with: " ")
                    if code.hasPrefix(" "), code.hasSuffix(" "), code.count > 2 {
                        code.removeFirst()
                        code.removeLast()
                    }
                    return (code, end)
                }
                scan = end
                continue
            }
            scan = text.index(after: scan)
        }
        return nil
    }

    private static func linkOrImage(_ text: String, at start: String.Index) -> (html: String, end: String.Index)? {
        var index = start
        let image = text[index] == "!"
        if image {
            index = text.index(after: index)
            guard index < text.endIndex, text[index] == "[" else { return nil }
        }
        guard let labelEnd = closingBracket(text, from: text.index(after: index)) else { return nil }
        let label = String(text[text.index(after: index)..<labelEnd])
        var destStart = text.index(after: labelEnd)
        guard destStart < text.endIndex, text[destStart] == "(" else { return nil }
        destStart = text.index(after: destStart)
        guard let destEnd = closingParen(text, from: destStart) else { return nil }
        var destination = String(text[destStart..<destEnd]).trimmingCharacters(in: .whitespaces)
        if let space = destination.firstIndex(where: { $0 == " " || $0 == "\t" || $0 == "\n" }) {
            destination = String(destination[..<space])
        }
        if destination.hasPrefix("<"), destination.hasSuffix(">"), destination.count >= 2 {
            destination.removeFirst()
            destination.removeLast()
        }
        let after = text.index(after: destEnd)
        let labelHTML = renderInline(label)
        guard let href = safeURL(destination) else {
            return (labelHTML, after)
        }
        if image {
            return ("<img src=\"\(href)\" alt=\"\(escape(stripTags(labelHTML)))\">", after)
        }
        return ("<a href=\"\(href)\">\(labelHTML)</a>", after)
    }

    private static func autolink(_ text: String, at start: String.Index) -> (html: String, end: String.Index)? {
        guard let close = text[start...].dropFirst().firstIndex(of: ">") else { return nil }
        let inner = String(text[text.index(after: start)..<close])
        guard !inner.isEmpty, !inner.contains(" "), !inner.contains("<") else { return nil }
        let end = text.index(after: close)
        let lower = inner.lowercased()
        if lower.hasPrefix("http://") || lower.hasPrefix("https://") || lower.hasPrefix("mailto:") {
            guard let href = safeURL(inner) else { return nil }
            return ("<a href=\"\(href)\">\(escape(inner))</a>", end)
        }
        let parts = inner.split(separator: "@", omittingEmptySubsequences: false)
        if parts.count == 2, !inner.contains(":"), parts.allSatisfy({ !$0.isEmpty }) {
            guard let href = safeURL("mailto:\(inner)") else { return nil }
            return ("<a href=\"\(href)\">\(escape(inner))</a>", end)
        }
        return nil
    }

    private static func emphasis(_ text: String, at start: String.Index) -> (html: String, end: String.Index)? {
        let character = text[start]
        guard character == "*" || character == "_" || character == "~" else { return nil }
        let run = runLength(text, at: start, character: character)
        let count: Int
        let tag: String
        if character == "~" {
            guard run >= 2 else { return nil }
            count = 2
            tag = "del"
        } else if run >= 2 {
            count = 2
            tag = "strong"
        } else {
            count = 1
            tag = "em"
        }
        let contentStart = text.index(start, offsetBy: count)
        guard contentStart < text.endIndex else { return nil }
        if character != "~", text[contentStart].isWhitespace { return nil }
        if character == "_", !canOpenUnderscore(text, at: start) { return nil }
        guard let close = findCloser(text, from: contentStart, character: character, count: count) else { return nil }
        if character == "_", !canCloseUnderscore(text, at: close) { return nil }
        let inner = String(text[contentStart..<close])
        guard !inner.isEmpty else { return nil }
        let end = text.index(close, offsetBy: count)
        return ("<\(tag)>\(renderInline(inner))</\(tag)>", end)
    }

    private static func findCloser(_ text: String, from start: String.Index, character: Character, count: Int) -> String.Index? {
        var index = start
        while index < text.endIndex {
            if text[index] == "\\" {
                let next = text.index(after: index)
                index = next < text.endIndex ? text.index(after: next) : next
                continue
            }
            if text[index] == "`", let span = codeSpan(text, at: index) {
                index = span.end
                continue
            }
            if text[index] == character {
                let run = runLength(text, at: index, character: character)
                if run == count {
                    let before = text.index(before: index)
                    if character == "~" || !text[before].isWhitespace {
                        return index
                    }
                }
                index = text.index(index, offsetBy: run, limitedBy: text.endIndex) ?? text.endIndex
                continue
            }
            index = text.index(after: index)
        }
        return nil
    }

    private static func runLength(_ text: String, at start: String.Index, character: Character) -> Int {
        var count = 0
        var index = start
        while index < text.endIndex, text[index] == character {
            count += 1
            index = text.index(after: index)
        }
        return count
    }

    private static func canOpenUnderscore(_ text: String, at index: String.Index) -> Bool {
        if index > text.startIndex {
            let previous = text[text.index(before: index)]
            if previous.isLetter || previous.isNumber { return false }
        }
        return true
    }

    private static func canCloseUnderscore(_ text: String, at index: String.Index) -> Bool {
        let after = text.index(index, offsetBy: runLength(text, at: index, character: "_"))
        guard after < text.endIndex else { return true }
        let next = text[after]
        return !(next.isLetter || next.isNumber)
    }

    private static func closingBracket(_ text: String, from start: String.Index) -> String.Index? {
        var index = start
        var escaped = false
        while index < text.endIndex {
            if escaped {
                escaped = false
                index = text.index(after: index)
                continue
            }
            if text[index] == "\\" {
                escaped = true
                index = text.index(after: index)
                continue
            }
            if text[index] == "]" { return index }
            if text[index] == "[" { return nil }
            index = text.index(after: index)
        }
        return nil
    }

    private static func closingParen(_ text: String, from start: String.Index) -> String.Index? {
        var index = start
        var depth = 0
        var escaped = false
        while index < text.endIndex {
            if escaped {
                escaped = false
                index = text.index(after: index)
                continue
            }
            if text[index] == "\\" {
                escaped = true
                index = text.index(after: index)
                continue
            }
            if text[index] == "(" { depth += 1 }
            if text[index] == ")" {
                if depth == 0 { return index }
                depth -= 1
            }
            index = text.index(after: index)
        }
        return nil
    }

    static func safeURL(_ raw: String) -> String? {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        guard trimmed.unicodeScalars.allSatisfy({ $0.value >= 32 && $0.value != 127 }) else { return nil }
        let lower = trimmed.lowercased()
        if lower.hasPrefix("http://") || lower.hasPrefix("https://") || lower.hasPrefix("mailto:") {
            return escape(trimmed)
        }
        if lower.contains(":"), !lower.hasPrefix("./"), !lower.hasPrefix("../") {
            let scheme = lower.split(separator: ":", maxSplits: 1).first.map(String.init) ?? ""
            if !scheme.isEmpty, scheme.allSatisfy({ $0.isLetter || $0.isNumber || $0 == "+" || $0 == "." || $0 == "-" }) {
                return nil
            }
        }
        if lower.hasPrefix("//") { return nil }
        return escape(trimmed)
    }

    // MARK: - Text helpers

    private static func isBlank(_ line: String) -> Bool {
        line.allSatisfy { $0 == " " || $0 == "\t" }
    }

    private static func splitIndent(_ line: String) -> (Int, Substring) {
        var columns = 0
        var index = line.startIndex
        while index < line.endIndex {
            if line[index] == " " {
                columns += 1
            } else if line[index] == "\t" {
                columns += 4
            } else {
                break
            }
            if columns > 32 { break }
            index = line.index(after: index)
        }
        return (columns, line[index...])
    }

    private static func indentColumns(_ line: String) -> Int {
        splitIndent(line).0
    }

    private static func stripIndent(_ line: String, columns: Int) -> String {
        var left = columns
        var index = line.startIndex
        while index < line.endIndex, left > 0 {
            if line[index] == " " {
                left -= 1
            } else if line[index] == "\t" {
                left -= 4
            } else {
                break
            }
            index = line.index(after: index)
        }
        return String(line[index...])
    }

    private static func isAsciiPunctuation(_ character: Character) -> Bool {
        guard let scalar = character.unicodeScalars.first, character.unicodeScalars.count == 1 else { return false }
        let value = scalar.value
        return (value >= 33 && value <= 47) || (value >= 58 && value <= 64) || (value >= 91 && value <= 96) || (value >= 123 && value <= 126)
    }

    private static func stripTags(_ html: String) -> String {
        var result = ""
        var inside = false
        for character in html {
            if character == "<" { inside = true; continue }
            if character == ">" { inside = false; continue }
            if !inside { result.append(character) }
        }
        return result
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&amp;", with: "&")
    }

    static func escape(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
    }
}
