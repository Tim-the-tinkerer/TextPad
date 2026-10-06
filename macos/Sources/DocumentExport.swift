import AppKit

enum DocumentExport {
    static func htmlData(fromPlainText text: String, title: String, preserveUTF8BOM: Bool = false) -> Data? {
        // A pasted web page is the document. Write it through so a browser
        // runs it, instead of showing the source inside a <pre> page.
        if isStandaloneHTMLDocument(text) {
            return utf8Data(text, preserveBOM: preserveUTF8BOM)
        }

        let normalized = text
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        let body = "<pre>\(htmlEscape(normalized))</pre>"
        let html = wrapHTMLDocument(body: body, title: title)
        return html.data(using: .utf8)
    }

    /// Renders Markdown to a browser page. An HTML document pasted into a
    /// Markdown buffer is still written through unchanged.
    static func htmlData(fromMarkdown text: String, title: String, preserveUTF8BOM: Bool = false) -> Data? {
        if isStandaloneHTMLDocument(text) {
            return utf8Data(text, preserveBOM: preserveUTF8BOM)
        }
        return Markdown.htmlDocument(from: text, title: title).data(using: .utf8)
    }

    /// Pass-through keeps a UTF-8 BOM the open file had. Generated pages do not gain one.
    private static func utf8Data(_ text: String, preserveBOM: Bool) -> Data? {
        guard let data = text.data(using: .utf8) else { return nil }
        guard preserveBOM, !text.hasPrefix("\u{FEFF}") else { return data }
        var marked = Data([0xEF, 0xBB, 0xBF])
        marked.append(data)
        return marked
    }

    /// True when the text is itself an HTML document: optional BOM, whitespace,
    /// comments, or an XML declaration, then `<!DOCTYPE html` or `<html`.
    static func isStandaloneHTMLDocument(_ text: String) -> Bool {
        var rest = Substring(text)
        if rest.first == "\u{FEFF}" {
            rest = rest.dropFirst()
        }

        while true {
            rest = rest.drop(while: { $0.isWhitespace })
            if rest.hasPrefix("<!--") {
                guard let end = rest.range(of: "-->") else { return false }
                rest = rest[end.upperBound...]
                continue
            }
            if rest.hasPrefix("<?") {
                guard let end = rest.range(of: "?>") else { return false }
                rest = rest[end.upperBound...]
                continue
            }
            break
        }

        if rest.prefix(9).lowercased() == "<!doctype" {
            let afterName = rest.dropFirst(9).drop(while: { $0.isWhitespace })
            guard afterName.prefix(4).lowercased() == "html" else { return false }
            let following = afterName.dropFirst(4).first
            return following == nil || following!.isWhitespace || following == ">"
        }

        if rest.prefix(5).lowercased() == "<html" {
            let following = rest.dropFirst(5).first
            return following == nil || following!.isWhitespace || following == ">"
        }

        return false
    }

    static func pdfData(fromPlainText text: String, fontSize: CGFloat) -> Data? {
        let textView = NSTextView(frame: NSRect(x: 0, y: 0, width: 540, height: 10_000))
        textView.isEditable = false
        textView.isSelectable = false
        textView.drawsBackground = true
        textView.backgroundColor = .white
        textView.textColor = .textColor
        textView.font = NSFont.monospacedSystemFont(ofSize: fontSize, weight: .regular)
        textView.textContainerInset = NSSize(width: 24, height: 24)
        textView.string = text
        textView.sizeToFit()
        return RichTextFormatting.pdfData(from: textView)
    }

    private static func wrapHTMLDocument(body: String, title: String) -> String {
        let escapedTitle = htmlEscape(title)
        return """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>\(escapedTitle)</title>
        <style>
        body { background:#fff; color:#0d0d12; margin:1.5em; font-family:Helvetica,Arial,sans-serif; line-height:1.4; }
        pre { white-space:pre-wrap; word-wrap:break-word; font-family:Menlo,Monaco,Consolas,monospace; }
        </style>
        </head>
        <body>
        \(body)
        </body>
        </html>
        """
    }

    private static func htmlEscape(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
    }
}