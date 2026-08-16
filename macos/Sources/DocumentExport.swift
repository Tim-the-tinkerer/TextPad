import AppKit

enum DocumentExport {
    static func htmlData(fromPlainText text: String, title: String) -> Data? {
        let normalized = text
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        let body = "<pre>\(htmlEscape(normalized))</pre>"
        let html = wrapHTMLDocument(body: body, title: title)
        return html.data(using: .utf8)
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