import Foundation

final class FileChangeMonitor {
    private var source: DispatchSourceFileSystemObject?
    private var watchedURL: URL?
    private var onChange: (() -> Void)?
    private var suppressUntil: Date?
    private var debounceWorkItem: DispatchWorkItem?

    func watch(url: URL, onChange: @escaping () -> Void) {
        stop()
        watchedURL = url
        self.onChange = onChange

        let descriptor = open(url.path, O_EVTONLY)
        guard descriptor >= 0 else { return }

        let source = DispatchSource.makeFileSystemObjectSource(
            fileDescriptor: descriptor,
            eventMask: [.write, .rename, .delete],
            queue: .main
        )
        source.setEventHandler { [weak self] in
            self?.scheduleNotify()
        }
        source.setCancelHandler {
            close(descriptor)
        }
        source.resume()
        self.source = source
    }

    func suppressBriefly() {
        suppressUntil = Date().addingTimeInterval(2)
    }

    func stop() {
        debounceWorkItem?.cancel()
        debounceWorkItem = nil
        source?.cancel()
        source = nil
        watchedURL = nil
        onChange = nil
    }

    deinit {
        stop()
    }

    private func scheduleNotify() {
        if let suppressUntil, Date() < suppressUntil {
            return
        }

        debounceWorkItem?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self else { return }
            if let suppressUntil = self.suppressUntil, Date() < suppressUntil {
                return
            }
            self.onChange?()
        }
        debounceWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.4, execute: work)
    }
}