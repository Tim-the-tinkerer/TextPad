import AppKit
import Foundation

final class EditorDocument: NSObject {
    var documentID = UUID()
    var fileURL: URL?
    var content: String = ""
    var rtfData: Data?
    var format: DocumentFormat = .plainText
    var isDirty = false
    var encoding: String.Encoding = .utf8
    var writesByteOrderMark = false
    var language: SyntaxLanguage = .plain
    var lineEnding: LineEnding = .lf
    var lineEndingPolicy: LineEndingPolicy = .preserve
    private var lastKnownDiskModDate: Date?

    var displayName: String {
        if let url = fileURL {
            return url.lastPathComponent + (isDirty ? " •" : "")
        }
        return "Untitled" + (isDirty ? " •" : "")
    }

    var windowTitle: String {
        if let url = fileURL {
            return url.lastPathComponent + (isDirty ? " — Edited" : "")
        }
        return "Untitled" + (isDirty ? " — Edited" : "")
    }

    var isRichText: Bool { format == .richText }

    var fileExtensionMatchesFormat: Bool {
        guard let url = fileURL else { return false }
        let ext = url.pathExtension.lowercased()
        if isRichText {
            return ext == "rtf"
        }
        return ext != "rtf" && ext != "rtfd"
    }

    var suggestedSaveFileName: String {
        let ext = format.defaultExtension
        if let url = fileURL {
            return url.deletingPathExtension().lastPathComponent + "." + ext
        }
        return "Untitled." + ext
    }

    static let maxLoadBytes = 256 * 1024 * 1024

    func load(from url: URL, encoding explicitEncoding: String.Encoding? = nil) throws {
        if url.pathExtension.lowercased() == "rtfd" || url.hasDirectoryPath {
            throw NSError(domain: "TextPad", code: 8, userInfo: [
                NSLocalizedDescriptionKey: "RTFD packages are not supported. Open or export the document as a standard .rtf file."
            ])
        }
        let data = try SafeFileReader.readData(from: url)
        guard data.count <= Self.maxLoadBytes else {
            throw NSError(domain: "TextPad", code: 7, userInfo: [
                NSLocalizedDescriptionKey: "File is too large to open (\(data.count / (1024 * 1024)) MB). Maximum is \(Self.maxLoadBytes / (1024 * 1024)) MB."
            ])
        }
        fileURL = url
        format = DocumentFormat.detect(from: url)
        language = format == .plainText ? SyntaxLanguage.detect(from: url) : .plain
        lastKnownDiskModDate = try? FileManager.default.attributesOfItem(atPath: url.path)[.modificationDate] as? Date

        if format == .richText {
            if let attributed = NSAttributedString(rtf: data, documentAttributes: nil) {
                content = attributed.string
                rtfData = data
                encoding = .utf8
            } else {
                throw NSError(domain: "TextPad", code: 1, userInfo: [NSLocalizedDescriptionKey: "Unable to read RTF file."])
            }
            lineEnding = .lf
        } else {
            let resolvedEncoding = explicitEncoding ?? DocumentEncodingSupport.detectEncoding(in: data)
            guard let useEncoding = resolvedEncoding,
                  let text = DocumentEncodingSupport.decode(data, encoding: useEncoding) else {
                throw NSError(domain: "TextPad", code: 1, userInfo: [NSLocalizedDescriptionKey: "Unable to decode file encoding."])
            }
            content = text
            rtfData = nil
            encoding = useEncoding
            writesByteOrderMark = DocumentEncodingSupport.hasBOM(data, for: useEncoding)
            lineEnding = LineEnding.detect(in: text)
            lineEndingPolicy = EditorPreferences.shared.lineEndingOnSave
        }
        isDirty = false
    }

    func updateLineEndingFromContent() {
        guard !isRichText else { return }
        lineEnding = LineEnding.detect(in: content)
    }

    func updateLineEndingIfNeeded(editedRange: NSRange, delta: Int, in text: String) {
        guard !isRichText else { return }
        guard text.utf16.count <= LargeFileSupport.largeDocumentThreshold else { return }
        guard LineEnding.editMayAffectLineEndings(editedRange: editedRange, delta: delta, in: text as NSString) else {
            return
        }
        lineEnding = LineEnding.detect(in: text)
    }

    func save(rtfData: Data? = nil, plainContent: String? = nil, to url: URL? = nil) throws {
        let target = url ?? fileURL
        guard let target else {
            throw NSError(domain: "TextPad", code: 2, userInfo: [NSLocalizedDescriptionKey: "No file location specified."])
        }

        try validateSaveTarget(target)

        let data: Data
        if format == .richText {
            guard let rtf = rtfData ?? self.rtfData else {
                throw NSError(domain: "TextPad", code: 3, userInfo: [NSLocalizedDescriptionKey: "No rich text content to save."])
            }
            data = rtf
        } else {
            let text = plainContent ?? content
            guard let encoded = DocumentEncodingSupport.encode(
                text,
                encoding: encoding,
                lineEndingPolicy: lineEndingPolicy,
                originalLineEnding: lineEnding,
                includeBOM: writesByteOrderMark
            ) else {
                throw NSError(domain: "TextPad", code: 3, userInfo: [NSLocalizedDescriptionKey: "Unable to encode file."])
            }
            data = encoded
            if let plainContent {
                content = plainContent
                lineEnding = lineEndingPolicy.resultingEnding(original: lineEnding)
            }
        }

        try data.write(to: target, options: .atomic)
        fileURL = target
        format = DocumentFormat.detect(from: target)
        language = format == .plainText ? SyntaxLanguage.detect(from: target) : .plain
        if let rtfData { self.rtfData = rtfData }
        isDirty = false
        lastKnownDiskModDate = try? FileManager.default.attributesOfItem(atPath: target.path)[.modificationDate] as? Date
        NSDocumentController.shared.noteNewRecentDocumentURL(target)
    }

    func revert() throws {
        guard let url = fileURL else { return }
        try load(from: url)
    }

    func reloadFromDisk() throws {
        guard let url = fileURL else { return }
        try load(from: url, encoding: encoding)
    }

    func hasChangedOnDisk() -> Bool {
        guard let url = fileURL, let lastKnownDiskModDate else { return false }
        guard let current = try? FileManager.default.attributesOfItem(atPath: url.path)[.modificationDate] as? Date else {
            return false
        }
        return current > lastKnownDiskModDate
    }

    func noteSavedToDisk() {
        guard let url = fileURL else { return }
        lastKnownDiskModDate = try? FileManager.default.attributesOfItem(atPath: url.path)[.modificationDate] as? Date
    }

    func applyEncodingSettings(encoding: String.Encoding, lineEndingPolicy: LineEndingPolicy) {
        self.encoding = encoding
        self.lineEndingPolicy = lineEndingPolicy
    }

    private func validateSaveTarget(_ url: URL) throws {
        let ext = url.pathExtension.lowercased()
        if isRichText {
            guard ext == "rtf" else {
                throw NSError(domain: "TextPad", code: 4, userInfo: [
                    NSLocalizedDescriptionKey: "Rich text documents must be saved with a .rtf extension."
                ])
            }
        } else if ext == "rtf" || ext == "rtfd" {
            throw NSError(domain: "TextPad", code: 4, userInfo: [
                NSLocalizedDescriptionKey: "Plain text cannot be saved with a rich text (.rtf) extension."
            ])
        }
    }

    func clone() -> EditorDocument {
        let copy = EditorDocument()
        copy.documentID = documentID
        copy.fileURL = fileURL
        copy.content = content
        copy.rtfData = rtfData
        copy.format = format
        copy.isDirty = isDirty
        copy.encoding = encoding
        copy.writesByteOrderMark = writesByteOrderMark
        copy.language = language
        copy.lineEnding = lineEnding
        copy.lineEndingPolicy = lineEndingPolicy
        return copy
    }

    static func fromAutoSave(_ entry: AutoSaveEntry) -> EditorDocument {
        let document = EditorDocument()
        document.documentID = entry.documentID
        if let path = entry.fileURL {
            document.fileURL = URL(fileURLWithPath: path)
        }
        document.content = entry.content
        if let encoded = entry.rtfDataBase64, let data = Data(base64Encoded: encoded) {
            document.rtfData = data
        }
        document.format = DocumentFormat(rawValue: entry.format) ?? .plainText
        document.encoding = TextEncoding.supported.first { $0.name == entry.encodingName }?.encoding ?? .utf8
        document.writesByteOrderMark = entry.writesByteOrderMark ?? false
        document.lineEnding = LineEnding(rawValue: entry.lineEnding) ?? .lf
        document.lineEndingPolicy = EditorPreferences.shared.lineEndingOnSave
        document.language = document.isRichText ? .plain : SyntaxLanguage.detect(from: document.fileURL)
        document.isDirty = true
        return document
    }
}
