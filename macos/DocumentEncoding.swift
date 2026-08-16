import Foundation

struct TextEncoding: Hashable {
    let encoding: String.Encoding
    let name: String

    init(encoding: String.Encoding, name: String) {
        self.encoding = encoding
        self.name = name
    }

    static let supported: [TextEncoding] = [
        TextEncoding(encoding: .utf8, name: "UTF-8"),
        TextEncoding(encoding: .utf16LittleEndian, name: "UTF-16 LE"),
        TextEncoding(encoding: .utf16BigEndian, name: "UTF-16 BE"),
        TextEncoding(encoding: .ascii, name: "ASCII"),
        TextEncoding(encoding: .isoLatin1, name: "ISO Latin-1"),
        TextEncoding(encoding: .windowsCP1252, name: "Windows Latin-1"),
        TextEncoding(encoding: .macOSRoman, name: "Mac Roman")
    ]

    static func named(_ encoding: String.Encoding) -> String {
        if let match = supported.first(where: { $0.encoding == encoding }) {
            return match.name
        }
        if encoding == .utf16 {
            return "UTF-16"
        }
        return "Unknown"
    }

    static func from(encoding: String.Encoding) -> TextEncoding {
        supported.first { $0.encoding == encoding } ?? TextEncoding(encoding: encoding, name: named(encoding))
    }
}

enum LineEnding: String, CaseIterable {
    case lf = "LF"
    case crlf = "CRLF"
    case cr = "CR"
    case mixed = "Mixed"

    var displayName: String { rawValue }

    var separator: String {
        switch self {
        case .lf: return "\n"
        case .crlf: return "\r\n"
        case .cr: return "\r"
        case .mixed: return "\n"
        }
    }

    static func detect(in text: String) -> LineEnding {
        var hasLF = false
        var hasCR = false
        var hasCRLF = false

        var index = text.startIndex
        while index < text.endIndex {
            let char = text[index]
            if char == "\r" {
                let next = text.index(after: index)
                if next < text.endIndex, text[next] == "\n" {
                    hasCRLF = true
                    index = text.index(after: next)
                    continue
                }
                hasCR = true
            } else if char == "\n" {
                hasLF = true
            }
            index = text.index(after: index)
        }

        if hasCRLF && (hasCR || hasLF) { return .mixed }
        if hasCRLF { return .crlf }
        if hasCR && hasLF { return .mixed }
        if hasCR { return .cr }
        if hasLF { return .lf }
        return .lf
    }

    static func editMayAffectLineEndings(editedRange: NSRange, delta: Int, in text: NSString) -> Bool {
        guard delta != 0 else { return false }
        let length = text.length
        guard length > 0 else { return delta > 0 }

        let safeRange = NSRange(
            location: max(0, min(editedRange.location, length)),
            length: max(0, min(editedRange.length, length - max(0, min(editedRange.location, length))))
        )

        if delta > 0 {
            let insertedLength = min(delta, length - safeRange.location)
            guard insertedLength > 0 else { return false }
            let inserted = text.substring(with: NSRange(location: safeRange.location, length: insertedLength))
            return inserted.rangeOfCharacter(from: .newlines) != nil
        }

        // Deletions can remove a newline; rescan only the affected line neighborhood.
        let neighborhood = text.lineRange(for: safeRange)
        return text.rangeOfCharacter(from: .newlines, options: [], range: neighborhood).location != NSNotFound
    }

    static func normalize(_ text: String, to ending: LineEnding) -> String {
        let unified = text
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        switch ending {
        case .lf, .mixed:
            return unified
        case .crlf:
            return unified.replacingOccurrences(of: "\n", with: "\r\n")
        case .cr:
            return unified.replacingOccurrences(of: "\n", with: "\r")
        }
    }
}

enum LineEndingPolicy: String, CaseIterable {
    case preserve
    case lf
    case crlf

    var displayName: String {
        switch self {
        case .preserve: return "Preserve"
        case .lf: return "Unix (LF)"
        case .crlf: return "Windows (CRLF)"
        }
    }

    func apply(to text: String, original: LineEnding) -> String {
        switch self {
        case .preserve:
            return text
        case .lf:
            return LineEnding.normalize(text, to: .lf)
        case .crlf:
            return LineEnding.normalize(text, to: .crlf)
        }
    }

    func resultingEnding(original: LineEnding) -> LineEnding {
        switch self {
        case .preserve: return original
        case .lf: return .lf
        case .crlf: return .crlf
        }
    }
}

enum DocumentEncodingSupport {
    static func hasBOM(_ data: Data, for encoding: String.Encoding) -> Bool {
        if encoding == .utf8 {
            return data.count >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF
        }
        if encoding == .utf16LittleEndian {
            return data.count >= 2 && data[0] == 0xFF && data[1] == 0xFE
        }
        if encoding == .utf16BigEndian {
            return data.count >= 2 && data[0] == 0xFE && data[1] == 0xFF
        }
        return false
    }

    static func detectEncoding(in data: Data) -> String.Encoding? {
        if data.isEmpty { return .utf8 }

        if data.count >= 3, data[0] == 0xEF, data[1] == 0xBB, data[2] == 0xBF {
            return .utf8
        }
        if data.count >= 2, data[0] == 0xFF, data[1] == 0xFE {
            return .utf16LittleEndian
        }
        if data.count >= 2, data[0] == 0xFE, data[1] == 0xFF {
            return .utf16BigEndian
        }

        if let utf16 = detectUTF16Endianness(in: data) {
            return utf16
        }

        if let text = String(data: data, encoding: .utf8), isReasonableUTF8(text, data: data) {
            return .utf8
        }

        if data.allSatisfy({ $0 < 0x80 }) {
            return .ascii
        }

        for encoding in [String.Encoding.windowsCP1252, .isoLatin1, .macOSRoman] {
            if let text = String(data: data, encoding: encoding), isPlausibleSingleByteText(text) {
                return encoding
            }
        }

        // Match Windows behavior: open undecodable bytes as ISO Latin-1 rather than failing.
        return .isoLatin1
    }

    static func stripBOM(from data: Data, encoding: String.Encoding) -> Data {
        if encoding == .utf8, data.count >= 3, data[0] == 0xEF, data[1] == 0xBB, data[2] == 0xBF {
            return data.dropFirst(3)
        }
        if data.count >= 2, data[0] == 0xFF, data[1] == 0xFE,
           encoding == .utf16LittleEndian || encoding == .utf16 {
            return data.dropFirst(2)
        }
        if data.count >= 2, data[0] == 0xFE, data[1] == 0xFF,
           encoding == .utf16BigEndian || encoding == .utf16 {
            return data.dropFirst(2)
        }
        return data
    }

    static func decode(_ data: Data, encoding: String.Encoding) -> String? {
        let resolved = resolveUTF16Encoding(encoding, in: data)
        let payload = stripBOM(from: data, encoding: resolved)
        return String(data: payload, encoding: resolved)
    }

    static func encode(_ text: String, encoding: String.Encoding, lineEndingPolicy: LineEndingPolicy, originalLineEnding: LineEnding, includeBOM: Bool) -> Data? {
        let normalized = lineEndingPolicy.apply(to: text, original: originalLineEnding)
        guard var payload = normalized.data(using: encoding, allowLossyConversion: false) else { return nil }
        if includeBOM, let bom = bom(for: encoding) {
            payload = bom + payload
        }
        return payload
    }

    private static func bom(for encoding: String.Encoding) -> Data? {
        switch encoding {
        case .utf16LittleEndian:
            return Data([0xFF, 0xFE])
        case .utf16BigEndian:
            return Data([0xFE, 0xFF])
        default:
            return nil
        }
    }

    private static func resolveUTF16Encoding(_ encoding: String.Encoding, in data: Data) -> String.Encoding {
        switch encoding {
        case .utf16LittleEndian, .utf16BigEndian:
            return encoding
        case .utf16:
            if data.count >= 2, data[0] == 0xFF, data[1] == 0xFE { return .utf16LittleEndian }
            if data.count >= 2, data[0] == 0xFE, data[1] == 0xFF { return .utf16BigEndian }
            return detectUTF16Endianness(in: data) ?? .utf16BigEndian
        default:
            return encoding
        }
    }

    private static func detectUTF16Endianness(in data: Data) -> String.Encoding? {
        guard data.count >= 4, data.count % 2 == 0 else { return nil }

        var zeroEven = 0
        var zeroOdd = 0
        let pairs = min(data.count / 2, 32 * 1024)
        for index in 0..<pairs {
            let even = data[index * 2]
            let odd = data[index * 2 + 1]
            if even == 0 && odd != 0 { zeroEven += 1 }
            if odd == 0 && even != 0 { zeroOdd += 1 }
        }

        let candidates: [String.Encoding]
        if zeroEven > zeroOdd * 2 {
            candidates = [.utf16BigEndian, .utf16LittleEndian]
        } else if zeroOdd > zeroEven * 2 {
            candidates = [.utf16LittleEndian, .utf16BigEndian]
        } else {
            return nil
        }

        guard max(zeroEven, zeroOdd) > max(2, pairs / 8) else { return nil }

        for encoding in candidates {
            if let text = String(data: data, encoding: encoding), isReasonableUTF16(text) {
                return encoding
            }
        }
        return nil
    }

    private static func isReasonableUTF8(_ text: String, data: Data) -> Bool {
        guard !text.isEmpty else { return true }
        if text.contains("\u{FFFD}") {
            return data.isEmpty
        }
        return true
    }

    private static func isReasonableUTF16(_ text: String) -> Bool {
        guard !text.isEmpty else { return true }
        if text.contains("\u{FFFD}") { return false }

        var suspicious = 0
        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 0x00...0x08, 0x0B, 0x0C, 0x0E...0x1F:
                suspicious += 2
            case 0x80...0x9F:
                suspicious += 1
            default:
                break
            }
        }
        return suspicious <= max(2, text.count / 100)
    }

    private static func isPlausibleSingleByteText(_ text: String) -> Bool {
        guard !text.isEmpty else { return true }
        if text.contains("\u{FFFD}") { return false }

        var suspicious = 0
        var meaningful = 0
        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 0x09, 0x0A, 0x0D, 0x20...0x7E, 0xA0...0xFF:
                meaningful += 1
            case 0x00...0x08, 0x0B, 0x0C, 0x0E...0x1F:
                suspicious += 2
            case 0x80...0x9F:
                suspicious += 1
            default:
                meaningful += 1
            }
        }

        let total = meaningful + suspicious
        guard total > 0 else { return true }
        return Double(suspicious) / Double(total) < 0.05
    }
}
