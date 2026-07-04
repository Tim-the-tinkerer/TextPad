import AppKit

final class FindReplaceController: NSWindowController {
    private let findField = NSTextField()
    private let replaceField = NSTextField()
    private let caseSensitiveCheckbox = NSButton(checkboxWithTitle: "Case Sensitive", target: nil, action: nil)
    private let wholeWordCheckbox = NSButton(checkboxWithTitle: "Whole Word", target: nil, action: nil)
    private let regexCheckbox = NSButton(checkboxWithTitle: "Regular Expression", target: nil, action: nil)
    private let statusLabel = NSTextField(labelWithString: "")

    weak var targetTextView: NSTextView?

    init() {
        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 480, height: 200),
            styleMask: [.titled, .closable, .utilityWindow],
            backing: .buffered,
            defer: false
        )
        panel.title = "Find & Replace"
        panel.isFloatingPanel = true
        super.init(window: panel)
        setupUI()
    }

    required init?(coder: NSCoder) { fatalError() }

    private func setupUI() {
        guard let content = window?.contentView else { return }

        let findLabel = NSTextField(labelWithString: "Find:")
        let replaceLabel = NSTextField(labelWithString: "Replace:")

        findField.placeholderString = "Search text"
        replaceField.placeholderString = "Replacement text"

        for field in [findField, replaceField] {
            field.translatesAutoresizingMaskIntoConstraints = false
            field.controlSize = .regular
        }

        caseSensitiveCheckbox.target = self
        caseSensitiveCheckbox.action = #selector(findNext)
        wholeWordCheckbox.target = self
        wholeWordCheckbox.action = #selector(findNext)
        regexCheckbox.target = self
        regexCheckbox.action = #selector(findNext)

        let findNextBtn = NSButton(title: "Find Next", target: self, action: #selector(findNext))
        let findPrevBtn = NSButton(title: "Find Previous", target: self, action: #selector(findPrevious))
        let replaceBtn = NSButton(title: "Replace", target: self, action: #selector(replace))
        let replaceAllBtn = NSButton(title: "Replace All", target: self, action: #selector(replaceAll))

        findNextBtn.bezelStyle = .rounded
        findPrevBtn.bezelStyle = .rounded
        replaceBtn.bezelStyle = .rounded
        replaceAllBtn.bezelStyle = .rounded
        findNextBtn.keyEquivalent = "\r"

        statusLabel.font = NSFont.systemFont(ofSize: 11)
        statusLabel.textColor = .secondaryLabelColor

        let checkboxStack = NSStackView(views: [caseSensitiveCheckbox, wholeWordCheckbox, regexCheckbox])
        checkboxStack.orientation = .horizontal
        checkboxStack.spacing = 16

        let buttonStack = NSStackView(views: [findPrevBtn, findNextBtn, replaceBtn, replaceAllBtn])
        buttonStack.orientation = .horizontal
        buttonStack.spacing = 8

        let grid = NSGridView(views: [
            [findLabel, findField],
            [replaceLabel, replaceField]
        ])
        grid.column(at: 0).xPlacement = .leading
        grid.column(at: 1).xPlacement = .fill
        grid.row(at: 0).topPadding = 16
        grid.row(at: 1).bottomPadding = 8

        let stack = NSStackView(views: [grid, checkboxStack, buttonStack, statusLabel])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 12
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.edgeInsets = NSEdgeInsets(top: 8, left: 16, bottom: 16, right: 16)

        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            stack.topAnchor.constraint(equalTo: content.topAnchor),
            stack.bottomAnchor.constraint(equalTo: content.bottomAnchor),
            findField.widthAnchor.constraint(greaterThanOrEqualToConstant: 300),
            replaceField.widthAnchor.constraint(equalTo: findField.widthAnchor)
        ])
    }

    func showPanel(for textView: NSTextView) {
        targetTextView = textView
        let sel = textView.selectedRange()
        if sel.length > 0 {
            let selected = (textView.string as NSString).substring(with: sel)
            if !selected.contains("\n") {
                findField.stringValue = selected
            }
        }
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        findField.becomeFirstResponder()
    }

    private func searchOptions() -> NSString.CompareOptions {
        var options: NSString.CompareOptions = []
        if caseSensitiveCheckbox.state == .off { options.insert(.caseInsensitive) }
        return options
    }

    private func findRange(forward: Bool) -> NSRange? {
        guard let textView = targetTextView else { return nil }
        let searchText = findField.stringValue
        guard !searchText.isEmpty else { return nil }

        let content = textView.string as NSString
        let start: Int
        if forward {
            start = textView.selectedRange().location + textView.selectedRange().length
        } else {
            start = max(0, textView.selectedRange().location - 1)
        }

        if regexCheckbox.state == .on {
            return findWithRegex(in: content, forward: forward, from: start)
        }

        let options = searchOptions()
        if wholeWordCheckbox.state == .on {
            return findWholeWord(in: content, searchText: searchText, forward: forward, from: start, options: options)
        }

        let backwardOptions = options.union(.backwards)
        let range = forward
            ? content.range(of: searchText, options: options, range: NSRange(location: start, length: content.length - start))
            : content.range(of: searchText, options: backwardOptions, range: NSRange(location: 0, length: start + 1))

        if range.location != NSNotFound { return range }

        // Wrap around
        let wrapRange = forward
            ? content.range(of: searchText, options: options)
            : content.range(of: searchText, options: backwardOptions)
        return wrapRange.location != NSNotFound ? wrapRange : nil
    }

    private func findWholeWord(in content: NSString, searchText: String, forward: Bool, from: Int, options: NSString.CompareOptions) -> NSRange? {
        let pattern = "\\b" + NSRegularExpression.escapedPattern(for: searchText) + "\\b"
        guard let regex = try? NSRegularExpression(pattern: pattern, options: options.contains(.caseInsensitive) ? .caseInsensitive : []) else { return nil }
        let searchRange = forward
            ? NSRange(location: from, length: content.length - from)
            : NSRange(location: 0, length: from + 1)
        let matches = regex.matches(in: content as String, range: searchRange)
        if forward, let last = matches.first { return last.range }
        if !forward, let last = matches.last { return last.range }
        let all = regex.matches(in: content as String, range: NSRange(location: 0, length: content.length))
        return forward ? all.first?.range : all.last?.range
    }

    private func findWithRegex(in content: NSString, forward: Bool, from: Int) -> NSRange? {
        let pattern = findField.stringValue
        guard let regex = try? NSRegularExpression(pattern: pattern, options: caseSensitiveCheckbox.state == .off ? .caseInsensitive : []) else {
            statusLabel.stringValue = "Invalid regular expression"
            return nil
        }
        let searchRange = forward
            ? NSRange(location: from, length: content.length - from)
            : NSRange(location: 0, length: from + 1)
        let matches = regex.matches(in: content as String, range: searchRange)
        if forward, let m = matches.first { return m.range }
        if !forward, let m = matches.last { return m.range }
        let all = regex.matches(in: content as String, range: NSRange(location: 0, length: content.length))
        return forward ? all.first?.range : all.last?.range
    }

    private func selectAndScroll(to range: NSRange) {
        guard let textView = targetTextView else { return }
        textView.setSelectedRange(range)
        textView.scrollRangeToVisible(range)
        textView.showFindIndicator(for: range)
        statusLabel.stringValue = "Found at line \((textView.string as NSString).substring(to: range.location).components(separatedBy: "\n").count)"
    }

    @objc func findNext() {
        guard let range = findRange(forward: true) else {
            statusLabel.stringValue = "Not found"
            NSSound.beep()
            return
        }
        selectAndScroll(to: range)
    }

    @objc func findPrevious() {
        guard let range = findRange(forward: false) else {
            statusLabel.stringValue = "Not found"
            NSSound.beep()
            return
        }
        selectAndScroll(to: range)
    }

    @objc private func replace() {
        guard let textView = targetTextView else { return }
        let selected = textView.selectedRange()
        let searchText = findField.stringValue
        guard !searchText.isEmpty else { return }

        if let range = findRange(forward: true), range.location == selected.location, range.length == selected.length || selected.length == 0 {
            textView.insertText(replaceField.stringValue, replacementRange: range)
            findNext()
        } else if let range = findRange(forward: true) {
            selectAndScroll(to: range)
        }
    }

    @objc private func replaceAll() {
        guard let textView = targetTextView, let storage = textView.textStorage else { return }
        let searchText = findField.stringValue
        let replaceText = replaceField.stringValue
        guard !searchText.isEmpty else { return }

        var count = 0
        let content = textView.string as NSString

        if regexCheckbox.state == .on, let regex = try? NSRegularExpression(pattern: searchText, options: caseSensitiveCheckbox.state == .off ? .caseInsensitive : []) {
            let matches = regex.matches(in: content as String, range: NSRange(location: 0, length: content.length)).reversed()
            storage.beginEditing()
            for match in matches {
                storage.replaceCharacters(in: match.range, with: replaceText)
                count += 1
            }
            storage.endEditing()
        } else {
            let options = searchOptions()
            var matches: [NSRange] = []
            var searchRange = NSRange(location: 0, length: content.length)
            while true {
                let range = content.range(of: searchText, options: options, range: searchRange)
                if range.location == NSNotFound { break }
                matches.append(range)
                searchRange = NSRange(location: NSMaxRange(range), length: content.length - NSMaxRange(range))
                if searchRange.length <= 0 { break }
            }

            storage.beginEditing()
            for range in matches.reversed() {
                storage.replaceCharacters(in: range, with: replaceText)
            }
            storage.endEditing()
            count = matches.count
        }

        statusLabel.stringValue = "Replaced \(count) occurrence\(count == 1 ? "" : "s")"
    }
}