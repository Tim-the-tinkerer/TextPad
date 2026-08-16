import Foundation

enum AppHelp {
    static let fileName = "Help.md"

    static var url: URL? {
        if let bundled = Bundle.main.url(forResource: "Help", withExtension: "md") {
            return bundled
        }

        let bundleResource = Bundle.main.resourceURL?.appendingPathComponent(fileName)
        if let bundleResource, FileManager.default.fileExists(atPath: bundleResource.path) {
            return bundleResource
        }

        let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let local = cwd.appendingPathComponent(fileName)
        if FileManager.default.fileExists(atPath: local.path) {
            return local
        }

        return nil
    }

    static var isAvailable: Bool {
        url != nil
    }
}