import AppKit

let launchPaths = Array(CommandLine.arguments.dropFirst())
let singleInstanceManager = SingleInstanceManager()

if !singleInstanceManager.tryBecomePrimary() {
    for attempt in 0..<30 {
        if SingleInstanceManager.forwardToRunningInstance(filePaths: launchPaths) {
            exit(0)
        }
        Thread.sleep(forTimeInterval: 0.2)
        if attempt == 29 {
            fputs("TextPad is already running, but the open request could not be delivered.\n", stderr)
            exit(1)
        }
    }
}

BundledFonts.register()

let app = NSApplication.shared
let delegate = AppDelegate(singleInstanceManager: singleInstanceManager)
app.delegate = delegate
app.run()