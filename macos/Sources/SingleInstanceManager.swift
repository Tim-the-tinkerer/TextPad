import Foundation

enum SingleInstanceCommand {
    case openFiles([String])
    case activate
}

final class SingleInstanceManager {
    private static let openCommand = "OPEN"
    private static let activateCommand = "ACTIVATE"

    private let socketPath: String
    private var serverFD: Int32 = -1
    private var listenSource: DispatchSourceRead?

    init() {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let directory = support.appendingPathComponent("com.textpad.editor", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        socketPath = directory.appendingPathComponent("instance.sock").path
    }

    deinit {
        stopListening()
    }

    func tryBecomePrimary() -> Bool {
        if Self.canConnect(to: socketPath) {
            return false
        }

        unlink(socketPath)

        serverFD = socket(AF_UNIX, SOCK_STREAM, 0)
        guard serverFD >= 0 else { return true }

        var address = sockaddr_un()
        address.sun_family = sa_family_t(AF_UNIX)
        _ = socketPath.withCString { cString in
            strncpy(&address.sun_path.0, cString, MemoryLayout.size(ofValue: address.sun_path) - 1)
        }

        let bindResult = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                bind(serverFD, $0, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }

        if bindResult != 0 {
            close(serverFD)
            serverFD = -1
            return false
        }

        guard listen(serverFD, 5) == 0 else {
            close(serverFD)
            serverFD = -1
            unlink(socketPath)
            return false
        }

        return true
    }

    private static func canConnect(to socketPath: String) -> Bool {
        let clientFD = socket(AF_UNIX, SOCK_STREAM, 0)
        guard clientFD >= 0 else { return false }
        defer { close(clientFD) }

        var address = sockaddr_un()
        address.sun_family = sa_family_t(AF_UNIX)
        _ = socketPath.withCString { cString in
            strncpy(&address.sun_path.0, cString, MemoryLayout.size(ofValue: address.sun_path) - 1)
        }

        let connectResult = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                connect(clientFD, $0, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }
        return connectResult == 0
    }

    func startListening(handler: @escaping (SingleInstanceCommand) -> Void) {
        guard serverFD >= 0, listenSource == nil else { return }

        let source = DispatchSource.makeReadSource(fileDescriptor: serverFD, queue: .global(qos: .userInitiated))
        source.setEventHandler { [weak self] in
            self?.acceptConnection(handler: handler)
        }
        source.setCancelHandler { [weak self] in
            guard let self else { return }
            if self.serverFD >= 0 {
                close(self.serverFD)
                self.serverFD = -1
            }
            unlink(self.socketPath)
        }
        source.resume()
        listenSource = source
    }

    func stopListening() {
        listenSource?.cancel()
        listenSource = nil
        if serverFD >= 0 {
            close(serverFD)
            serverFD = -1
        }
        unlink(socketPath)
    }

    static func forwardToRunningInstance(filePaths: [String]) -> Bool {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let socketPath = support
            .appendingPathComponent("com.textpad.editor", isDirectory: true)
            .appendingPathComponent("instance.sock")
            .path

        let clientFD = socket(AF_UNIX, SOCK_STREAM, 0)
        guard clientFD >= 0 else { return false }
        defer { close(clientFD) }

        var address = sockaddr_un()
        address.sun_family = sa_family_t(AF_UNIX)
        _ = socketPath.withCString { cString in
            strncpy(&address.sun_path.0, cString, MemoryLayout.size(ofValue: address.sun_path) - 1)
        }

        let connectResult = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                connect(clientFD, $0, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }
        guard connectResult == 0 else { return false }

        var payload = ""
        if filePaths.isEmpty {
            payload = "\(activateCommand)\n\n"
        } else {
            payload = "\(openCommand)\n"
            for path in filePaths where !path.isEmpty {
                payload += "\(path)\n"
            }
            payload += "\n"
        }

        return payload.withCString { cString in
            write(clientFD, cString, strlen(cString)) >= 0
        }
    }

    private func acceptConnection(handler: @escaping (SingleInstanceCommand) -> Void) {
        let clientFD = accept(serverFD, nil, nil)
        guard clientFD >= 0 else { return }
        defer { close(clientFD) }

        var buffer = [UInt8](repeating: 0, count: 65536)
        let bytesRead = read(clientFD, &buffer, buffer.count)
        guard bytesRead > 0 else { return }

        let message = String(bytes: buffer.prefix(bytesRead), encoding: .utf8) ?? ""
        guard let command = parse(message) else { return }

        DispatchQueue.main.async {
            handler(command)
        }
    }

    private func parse(_ message: String) -> SingleInstanceCommand? {
        var lines = message.split(whereSeparator: \.isNewline).map(String.init)
        guard let command = lines.first, !command.isEmpty else { return nil }
        lines.removeFirst()
        while let last = lines.last, last.isEmpty {
            lines.removeLast()
        }

        switch command {
        case Self.openCommand:
            return .openFiles(lines)
        case Self.activateCommand:
            return .activate
        default:
            return nil
        }
    }
}