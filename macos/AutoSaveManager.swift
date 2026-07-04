import Foundation

struct AutoSaveEntry: Codable {
    let documentID: UUID
    let fileURL: String?
    let displayName: String
    let content: String
    let encodingName: String
    let lineEnding: String
    let savedAt: Date
    let format: String
    let rtfDataBase64: String?
}

final class AutoSaveManager {
    private weak var editor: EditorViewController?
    private var timer: Timer?
    private let fileManager = FileManager.default

    init(editor: EditorViewController) {
        self.editor = editor
    }

    func start() {
        stop()
        guard EditorPreferences.shared.autoSaveEnabled else { return }
        let interval = TimeInterval(EditorPreferences.shared.autoSaveInterval)
        let timer = Timer(timeInterval: interval, repeats: true) { [weak self] _ in
            self?.writeSnapshotIfNeeded()
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    func writeSnapshotIfNeeded() {
        guard let editor else { return }
        let document = editor.document
        guard document.isDirty || document.fileURL == nil else { return }
        editor.syncDocument()

        let rtfBase64: String?
        if document.isRichText, let rtf = document.rtfData {
            rtfBase64 = rtf.base64EncodedString()
        } else {
            rtfBase64 = nil
        }

        let entry = AutoSaveEntry(
            documentID: document.documentID,
            fileURL: document.fileURL?.path,
            displayName: document.displayName.replacingOccurrences(of: " •", with: ""),
            content: document.content,
            encodingName: TextEncoding.named(document.encoding),
            lineEnding: document.lineEnding.rawValue,
            savedAt: Date(),
            format: document.format.rawValue,
            rtfDataBase64: rtfBase64
        )

        do {
            let url = try Self.fileURL(for: document.documentID)
            let data = try JSONEncoder().encode(entry)
            try data.write(to: url, options: .atomic)
        } catch {
            // Auto-save is best-effort.
        }
    }

    func clearSnapshot() {
        guard let editor else { return }
        Self.clear(documentID: editor.document.documentID)
    }

    static var autosaveDirectory: URL {
        let base = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
            .appendingPathComponent("com.textpad.editor", isDirectory: true)
            .appendingPathComponent("Autosave", isDirectory: true)
        try? fileManager.createDirectory(at: base, withIntermediateDirectories: true)
        return base
    }

    private static let fileManager = FileManager.default

    static func fileURL(for documentID: UUID) throws -> URL {
        autosaveDirectory.appendingPathComponent("\(documentID.uuidString).json")
    }

    static func clear(documentID: UUID) {
        try? fileManager.removeItem(at: autosaveDirectory.appendingPathComponent("\(documentID.uuidString).json"))
    }

    static func pendingRecoveries() -> [AutoSaveEntry] {
        guard let files = try? fileManager.contentsOfDirectory(at: autosaveDirectory, includingPropertiesForKeys: nil) else {
            return []
        }
        let decoder = JSONDecoder()
        return files
            .filter { $0.pathExtension == "json" }
            .compactMap { url -> AutoSaveEntry? in
                guard let data = try? Data(contentsOf: url) else { return nil }
                return try? decoder.decode(AutoSaveEntry.self, from: data)
            }
            .sorted { $0.savedAt > $1.savedAt }
    }

    deinit {
        stop()
    }
}