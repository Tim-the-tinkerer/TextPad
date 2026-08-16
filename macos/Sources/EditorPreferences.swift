import AppKit
import Foundation

enum EditorTheme: String, CaseIterable {
    case system
    case light
    case dark
    case solarized
    case sepia

    var displayName: String {
        switch self {
        case .system: return "System"
        case .light: return "Light"
        case .dark: return "Dark"
        case .solarized: return "Solarized"
        case .sepia: return "Sepia"
        }
    }

    var background: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.background
        case .light: return NSColor(calibratedWhite: 1.0, alpha: 1.0)
        case .dark: return NSColor(calibratedRed: 0.15, green: 0.15, blue: 0.17, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.02, green: 0.19, blue: 0.24, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.96, green: 0.93, blue: 0.86, alpha: 1)
        }
    }

    static var systemResolved: EditorTheme {
        let match = NSApp.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua])
        return match == .darkAqua ? .dark : .light
    }

    var text: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.text
        case .light: return NSColor(calibratedRed: 0.05, green: 0.05, blue: 0.07, alpha: 1)
        case .dark: return NSColor(calibratedWhite: 1.0, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.94, green: 0.95, blue: 0.92, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.12, green: 0.08, blue: 0.05, alpha: 1)
        }
    }

    var lineNumberBackground: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.lineNumberBackground
        case .light: return NSColor(calibratedRed: 0.95, green: 0.95, blue: 0.97, alpha: 1)
        case .dark: return NSColor(calibratedRed: 0.08, green: 0.08, blue: 0.10, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.0, green: 0.13, blue: 0.16, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.92, green: 0.88, blue: 0.80, alpha: 1)
        }
    }

    var lineNumberText: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.lineNumberText
        case .light: return NSColor(calibratedRed: 0.36, green: 0.36, blue: 0.40, alpha: 1)
        case .dark: return NSColor(calibratedWhite: 0.82, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.78, green: 0.81, blue: 0.77, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.38, green: 0.30, blue: 0.22, alpha: 1)
        }
    }

    /// High-readability text for status bars and secondary chrome on dark themes.
    var chromeText: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.chromeText
        case .light, .sepia: return lineNumberText
        case .dark, .solarized: return text
        }
    }

    var tabBarBackground: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.tabBarBackground
        case .light: return NSColor(calibratedRed: 0.94, green: 0.94, blue: 0.96, alpha: 1)
        case .dark: return NSColor(calibratedRed: 0.10, green: 0.10, blue: 0.12, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.0, green: 0.13, blue: 0.16, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.90, green: 0.86, blue: 0.78, alpha: 1)
        }
    }

    var tabText: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.tabText
        case .light: return NSColor(calibratedRed: 0.30, green: 0.30, blue: 0.34, alpha: 1)
        case .dark: return NSColor(calibratedWhite: 0.86, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.80, green: 0.83, blue: 0.79, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.42, green: 0.34, blue: 0.26, alpha: 1)
        }
    }

    var tabTextSelected: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.tabTextSelected
        case .light: return NSColor(calibratedRed: 0.05, green: 0.05, blue: 0.07, alpha: 1)
        case .dark: return NSColor(calibratedWhite: 1.0, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.97, green: 0.98, blue: 0.95, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.12, green: 0.08, blue: 0.05, alpha: 1)
        }
    }

    var selection: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.selection
        case .light: return NSColor(calibratedRed: 0.45, green: 0.68, blue: 1.0, alpha: 1)
        case .dark: return NSColor(calibratedRed: 0.16, green: 0.45, blue: 0.92, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.0, green: 0.45, blue: 0.58, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.72, green: 0.52, blue: 0.18, alpha: 1)
        }
    }

    var currentLineHighlight: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.currentLineHighlight
        case .light: return NSColor(calibratedRed: 0.90, green: 0.93, blue: 0.98, alpha: 1)
        case .dark: return NSColor(calibratedRed: 0.22, green: 0.24, blue: 0.28, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.04, green: 0.26, blue: 0.31, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.90, green: 0.86, blue: 0.78, alpha: 1)
        }
    }

    var isDark: Bool {
        switch self {
        case .system: return EditorTheme.systemResolved.isDark
        case .light, .sepia: return false
        case .dark, .solarized: return true
        }
    }

    var uiLabel: NSColor { text }

    var uiSectionTitle: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.uiSectionTitle
        case .light: return NSColor(calibratedRed: 0.02, green: 0.02, blue: 0.04, alpha: 1)
        case .dark: return NSColor(calibratedWhite: 1.0, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.93, green: 0.91, blue: 0.85, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.10, green: 0.06, blue: 0.03, alpha: 1)
        }
    }

    var uiControlBackground: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.uiControlBackground
        case .light: return NSColor(calibratedRed: 0.97, green: 0.97, blue: 0.99, alpha: 1)
        case .dark: return NSColor(calibratedRed: 0.22, green: 0.22, blue: 0.26, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.04, green: 0.26, blue: 0.31, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.99, green: 0.97, blue: 0.92, alpha: 1)
        }
    }

    var uiControlBorder: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.uiControlBorder
        case .light: return NSColor(calibratedRed: 0.78, green: 0.78, blue: 0.82, alpha: 1)
        case .dark: return NSColor(calibratedWhite: 0.48, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.48, green: 0.56, blue: 0.58, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.68, green: 0.60, blue: 0.48, alpha: 1)
        }
    }

    var uiAccent: NSColor {
        switch self {
        case .system: return EditorTheme.systemResolved.uiAccent
        case .light: return NSColor(calibratedRed: 0.0, green: 0.45, blue: 0.95, alpha: 1)
        case .dark: return NSColor(calibratedRed: 0.35, green: 0.62, blue: 1.0, alpha: 1)
        case .solarized: return NSColor(calibratedRed: 0.15, green: 0.55, blue: 0.82, alpha: 1)
        case .sepia: return NSColor(calibratedRed: 0.52, green: 0.32, blue: 0.12, alpha: 1)
        }
    }
}

final class EditorPreferences {
    static let shared = EditorPreferences()

    private let defaults = UserDefaults.standard

    var fontName: String {
        get { defaults.string(forKey: "fontName") ?? "Menlo" }
        set { defaults.set(newValue, forKey: "fontName"); notifyChange() }
    }

    var fontSize: CGFloat {
        get {
            let size = defaults.double(forKey: "fontSize")
            let resolved = size > 0 ? CGFloat(size) : 13
            return min(72, max(8, resolved))
        }
        set { defaults.set(Double(min(72, max(8, newValue))), forKey: "fontSize"); notifyChange() }
    }

    var tabWidth: Int {
        get {
            let width = defaults.integer(forKey: "tabWidth")
            return width > 0 ? width : 4
        }
        set { defaults.set(newValue, forKey: "tabWidth"); notifyChange() }
    }

    var theme: EditorTheme {
        get {
            let raw = defaults.string(forKey: "theme") ?? "system"
            return EditorTheme(rawValue: raw) ?? .system
        }
        set { defaults.set(newValue.rawValue, forKey: "theme"); notifyChange() }
    }

    var effectiveTheme: EditorTheme {
        let selected = theme
        return selected == .system ? EditorTheme.systemResolved : selected
    }

    func validate() {
        _ = fontSize
        _ = tabWidth
        _ = theme
    }

    var showLineNumbers: Bool {
        get { defaults.object(forKey: "showLineNumbers") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "showLineNumbers"); notifyChange() }
    }

    var wordWrap: Bool {
        get { defaults.object(forKey: "wordWrap") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "wordWrap"); notifyChange() }
    }

    var showInvisibles: Bool {
        get { defaults.object(forKey: "showInvisibles") as? Bool ?? false }
        set { defaults.set(newValue, forKey: "showInvisibles"); notifyChange() }
    }

    var showCurrentLineHighlight: Bool {
        get { defaults.object(forKey: "showCurrentLineHighlight") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "showCurrentLineHighlight"); notifyChange() }
    }

    var defaultEncoding: String.Encoding {
        get {
            let name = defaults.string(forKey: "defaultEncoding") ?? "UTF-8"
            return TextEncoding.supported.first { $0.name == name }?.encoding ?? .utf8
        }
        set {
            defaults.set(TextEncoding.named(newValue), forKey: "defaultEncoding")
            notifyChange()
        }
    }

    var lineEndingOnSave: LineEndingPolicy {
        get {
            let raw = defaults.string(forKey: "lineEndingOnSave") ?? LineEndingPolicy.preserve.rawValue
            return LineEndingPolicy(rawValue: raw) ?? .preserve
        }
        set { defaults.set(newValue.rawValue, forKey: "lineEndingOnSave"); notifyChange() }
    }

    var autoSaveEnabled: Bool {
        get { defaults.object(forKey: "autoSaveEnabled") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "autoSaveEnabled"); notifyChange() }
    }

    var autoSaveInterval: Int {
        get {
            let value = defaults.integer(forKey: "autoSaveInterval")
            return value > 0 ? value : 60
        }
        set { defaults.set(max(15, newValue), forKey: "autoSaveInterval"); notifyChange() }
    }

    var font: NSFont {
        BundledFonts.font(named: fontName, size: fontSize)
            ?? NSFont.monospacedSystemFont(ofSize: fontSize, weight: .regular)
    }

    static let didChangeNotification = Notification.Name("EditorPreferencesDidChange")

    private func notifyChange() {
        NotificationCenter.default.post(name: Self.didChangeNotification, object: nil)
    }
}