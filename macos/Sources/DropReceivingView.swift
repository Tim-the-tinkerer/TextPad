import AppKit
import UniformTypeIdentifiers

final class DropReceivingView: NSView {
    var onFilesDropped: (([URL]) -> Void)?

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        registerForDraggedTypes([.fileURL])
    }

    required init?(coder: NSCoder) { fatalError() }

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        containsFileURLs(sender) ? .copy : []
    }

    override func prepareForDragOperation(_ sender: NSDraggingInfo) -> Bool {
        containsFileURLs(sender)
    }

    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        guard let urls = fileURLs(from: sender), !urls.isEmpty else { return false }
        onFilesDropped?(urls)
        return true
    }

    private func containsFileURLs(_ sender: NSDraggingInfo) -> Bool {
        !(fileURLs(from: sender)?.isEmpty ?? true)
    }

    private func fileURLs(from sender: NSDraggingInfo) -> [URL]? {
        let options: [NSPasteboard.ReadingOptionKey: Any] = [
            .urlReadingFileURLsOnly: true,
            .urlReadingContentsConformToTypes: [UTType.fileURL.identifier]
        ]
        guard let urls = sender.draggingPasteboard.readObjects(forClasses: [NSURL.self], options: options) as? [URL],
              !urls.isEmpty else { return nil }
        return urls.filter { $0.isFileURL }
    }
}