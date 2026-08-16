import AppKit

struct TextSearchOptions {
    var caseSensitive = false
    var wholeWord = false
    var regularExpression = false
}

enum TextSearch {
    static func find(
        _ searchText: String,
        in content: NSString,
        from location: Int,
        forward: Bool,
        options: TextSearchOptions
    ) -> NSRange? {
        guard !searchText.isEmpty else { return nil }

        let start: Int
        if forward {
            start = max(0, min(location, content.length))
        } else {
            start = max(0, min(location, content.length - 1))
        }

        if options.regularExpression {
            return findWithRegex(searchText, in: content, forward: forward, from: start, caseSensitive: options.caseSensitive)
        }

        var compareOptions: NSString.CompareOptions = []
        if !options.caseSensitive { compareOptions.insert(.caseInsensitive) }

        if options.wholeWord {
            return findWholeWord(searchText, in: content, forward: forward, from: start, options: compareOptions)
        }

        let backwardOptions = compareOptions.union(.backwards)
        let range = forward
            ? content.range(of: searchText, options: compareOptions, range: NSRange(location: start, length: content.length - start))
            : content.range(of: searchText, options: backwardOptions, range: NSRange(location: 0, length: start + 1))

        if range.location != NSNotFound { return range }

        let wrapRange = forward
            ? content.range(of: searchText, options: compareOptions)
            : content.range(of: searchText, options: backwardOptions)
        return wrapRange.location != NSNotFound ? wrapRange : nil
    }

    static func countMatches(_ searchText: String, in content: NSString, options: TextSearchOptions) -> Int {
        guard !searchText.isEmpty else { return 0 }

        if options.regularExpression,
           let regex = try? NSRegularExpression(
               pattern: searchText,
               options: options.caseSensitive ? [] : .caseInsensitive
           ) {
            return regex.numberOfMatches(in: content as String, range: NSRange(location: 0, length: content.length))
        }

        var compareOptions: NSString.CompareOptions = []
        if !options.caseSensitive { compareOptions.insert(.caseInsensitive) }

        var count = 0
        var searchRange = NSRange(location: 0, length: content.length)
        while searchRange.length > 0 {
            let range = content.range(of: searchText, options: compareOptions, range: searchRange)
            if range.location == NSNotFound { break }
            count += 1
            searchRange = NSRange(location: NSMaxRange(range), length: content.length - NSMaxRange(range))
        }
        return count
    }

    static func matchNumber(at range: NSRange, for searchText: String, in content: NSString, options: TextSearchOptions) -> Int {
        guard !searchText.isEmpty else { return 0 }
        var number = 0
        var location = 0
        while location <= range.location {
            guard let match = find(searchText, in: content, from: location, forward: true, options: options) else {
                break
            }
            number += 1
            if match.location == range.location { return number }
            location = NSMaxRange(match)
            if match.length == 0 { break }
        }
        return max(number, 1)
    }

    private static func findWholeWord(
        _ searchText: String,
        in content: NSString,
        forward: Bool,
        from: Int,
        options: NSString.CompareOptions
    ) -> NSRange? {
        let pattern = "\\b" + NSRegularExpression.escapedPattern(for: searchText) + "\\b"
        guard let regex = try? NSRegularExpression(
            pattern: pattern,
            options: options.contains(.caseInsensitive) ? .caseInsensitive : []
        ) else { return nil }

        let searchRange = forward
            ? NSRange(location: from, length: content.length - from)
            : NSRange(location: 0, length: from + 1)
        let matches = regex.matches(in: content as String, range: searchRange)
        if forward, let first = matches.first { return first.range }
        if !forward, let last = matches.last { return last.range }

        let all = regex.matches(in: content as String, range: NSRange(location: 0, length: content.length))
        return forward ? all.first?.range : all.last?.range
    }

    private static func findWithRegex(
        _ pattern: String,
        in content: NSString,
        forward: Bool,
        from: Int,
        caseSensitive: Bool
    ) -> NSRange? {
        guard let regex = try? NSRegularExpression(
            pattern: pattern,
            options: caseSensitive ? [] : .caseInsensitive
        ) else { return nil }

        let searchRange = forward
            ? NSRange(location: from, length: content.length - from)
            : NSRange(location: 0, length: from + 1)
        let matches = regex.matches(in: content as String, range: searchRange)
        if forward, let first = matches.first { return first.range }
        if !forward, let last = matches.last { return last.range }

        let all = regex.matches(in: content as String, range: NSRange(location: 0, length: content.length))
        return forward ? all.first?.range : all.last?.range
    }
}