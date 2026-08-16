import AppKit
import CoreText

enum BundledFonts {
    static let familyNames = ["Interlac", "Interlac Unicode"]

    static func register() {
        for url in fontURLs() {
            var error: Unmanaged<CFError>?
            CTFontManagerRegisterFontsForURL(url as CFURL, .process, &error)
        }
    }

    static func font(named name: String, size: CGFloat) -> NSFont? {
        if let font = NSFont(name: name, size: size) {
            return font
        }
        return NSFontManager.shared.font(withFamily: name, traits: [], weight: 5, size: size)
    }

    private static func fontURLs() -> [URL] {
        guard let directory = Bundle.main.resourceURL?.appendingPathComponent("Fonts", isDirectory: true),
              let files = try? FileManager.default.contentsOfDirectory(
                at: directory,
                includingPropertiesForKeys: nil
              ) else { return [] }
        return files.filter { ["ttf", "otf"].contains($0.pathExtension.lowercased()) }
    }
}
