import Foundation

enum SafeFileReader {
    private static let maxAttempts = 5

    static func readData(from url: URL) throws -> Data {
        guard FileManager.default.fileExists(atPath: url.path) else {
            throw NSError(domain: NSCocoaErrorDomain, code: NSFileReadNoSuchFileError, userInfo: [
                NSLocalizedDescriptionKey: "File not found."
            ])
        }

        var lastError: Error?
        for attempt in 0..<maxAttempts {
            if attempt > 0 {
                Thread.sleep(forTimeInterval: 0.05 * Double(attempt))
            }

            do {
                let sizeBefore = try FileManager.default.attributesOfItem(atPath: url.path)[.size] as? Int64 ?? 0
                let data = try Data(contentsOf: url)
                let sizeAfter = try FileManager.default.attributesOfItem(atPath: url.path)[.size] as? Int64 ?? 0
                if Int64(data.count) == sizeBefore, Int64(data.count) == sizeAfter {
                    return data
                }
            } catch {
                lastError = error
            }
        }

        if let lastError {
            throw NSError(domain: "TextPad", code: 6, userInfo: [
                NSLocalizedDescriptionKey: "Unable to read a stable copy of \"\(url.lastPathComponent)\".",
                NSUnderlyingErrorKey: lastError
            ])
        }

        return try Data(contentsOf: url)
    }
}