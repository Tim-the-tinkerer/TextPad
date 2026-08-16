import Foundation

enum DocumentFormat: String, CaseIterable {
    case plainText
    case richText

    var displayName: String {
        switch self {
        case .plainText: return "Plain Text"
        case .richText: return "Rich Text"
        }
    }

    static func detect(from url: URL?) -> DocumentFormat {
        guard let ext = url?.pathExtension.lowercased() else { return .plainText }
        switch ext {
        case "rtf": return .richText
        default: return .plainText
        }
    }

    var defaultExtension: String {
        switch self {
        case .plainText: return "txt"
        case .richText: return "rtf"
        }
    }
}
