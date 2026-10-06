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
                let sizeBefore = try Self.byteCount(of: url)
                let data = try Data(contentsOf: url)
                let sizeAfter = try Self.byteCount(of: url)
                if Int64(data.count) == sizeBefore, Int64(data.count) == sizeAfter {
                    return data
                }
            } catch {
                lastError = error
            }
        }

        let underlying = lastError ?? NSError(domain: "TextPad", code: 6, userInfo: [
            NSLocalizedDescriptionKey: "The file changed while it was being read."
        ])
        throw NSError(domain: "TextPad", code: 6, userInfo: [
            NSLocalizedDescriptionKey: "Unable to read a stable copy of \"\(url.lastPathComponent)\".",
            NSUnderlyingErrorKey: underlying
        ])
    }

    private static func byteCount(of url: URL) throws -> Int64 {
        // URL resource values are cached on the URL, so a file that changes
        // during the read can look the same size on every attempt.
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        if let number = attributes[.size] as? NSNumber {
            return number.int64Value
        }
        if let size = attributes[.size] as? Int {
            return Int64(size)
        }
        if let size = attributes[.size] as? UInt64 {
            return Int64(size)
        }
        throw NSError(domain: "TextPad", code: 6, userInfo: [
            NSLocalizedDescriptionKey: "Unable to read the file size."
        ])
    }
}