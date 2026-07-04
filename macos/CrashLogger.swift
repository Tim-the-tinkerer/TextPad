import Foundation

private func textPadUncaughtExceptionHandler(_ exception: NSException) {
    CrashLogger.log(exception: exception, source: "Uncaught")
}

enum CrashLogger {
    private static var logURL: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return support
            .appendingPathComponent("com.textpad.editor", isDirectory: true)
            .appendingPathComponent("crash.log")
    }

    static func install() {
        NSSetUncaughtExceptionHandler(textPadUncaughtExceptionHandler)
    }

    static func log(exception: NSException, source: String) {
        let entry = """
        [\(isoTimestamp())] \(source)
        \(exception.name.rawValue): \(exception.reason ?? "unknown")
        \(exception.callStackSymbols.joined(separator: "\n"))

        """
        append(entry)
    }

    static func log(error: Error, source: String) {
        let entry = """
        [\(isoTimestamp())] \(source)
        \(error)

        """
        append(entry)
    }

    private static func append(_ entry: String) {
        do {
            let directory = logURL.deletingLastPathComponent()
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            if FileManager.default.fileExists(atPath: logURL.path) {
                let handle = try FileHandle(forWritingTo: logURL)
                defer { try? handle.close() }
                try handle.seekToEnd()
                if let data = entry.data(using: .utf8) {
                    try handle.write(contentsOf: data)
                }
            } else {
                try entry.write(to: logURL, atomically: true, encoding: .utf8)
            }
        } catch {
            // Best-effort logging.
        }
    }

    private static func isoTimestamp() -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.string(from: Date())
    }
}