import Foundation

final class ClosedTabManager {
    static let shared = ClosedTabManager()

    private var stack: [EditorDocument] = []
    private let maxCount = 20

    var canReopen: Bool { !stack.isEmpty }

    func recordClosed(document: EditorDocument) {
        stack.insert(document.clone(), at: 0)
        if stack.count > maxCount {
            stack.removeLast()
        }
    }

    func pop() -> EditorDocument? {
        guard !stack.isEmpty else { return nil }
        return stack.removeFirst()
    }
}