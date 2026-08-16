import AppKit

/// Draws the current-line band inside the text layout pass so it sits behind glyphs.
final class LineHighlightLayoutManager: NSLayoutManager {
    weak var highlightTextView: NSTextView?
    var highlightColor: NSColor = .clear
    var isHighlightEnabled = false
    private var lastHighlightedLineRange = NSRange(location: NSNotFound, length: 0)

    private static let accentWidth: CGFloat = 3

    func currentLineCharacterRange() -> NSRange {
        guard let textView = highlightTextView else { return NSRange(location: 0, length: 0) }
        let string = textView.string as NSString
        let location = min(textView.selectedRange().location, string.length)
        return string.lineRange(for: NSRange(location: location, length: 0))
    }

    func invalidateCurrentLineHighlight() {
        guard let textView = highlightTextView else { return }
        let range = currentLineCharacterRange()

        if lastHighlightedLineRange.location != NSNotFound {
            let oldGlyphRange = glyphRange(forCharacterRange: lastHighlightedLineRange, actualCharacterRange: nil)
            invalidateDisplay(forGlyphRange: oldGlyphRange)
        }

        if isHighlightEnabled {
            let glyphRange = glyphRange(forCharacterRange: range, actualCharacterRange: nil)
            invalidateDisplay(forGlyphRange: glyphRange)
            lastHighlightedLineRange = range
        } else {
            lastHighlightedLineRange = NSRange(location: NSNotFound, length: 0)
        }

        textView.setNeedsDisplay(textView.visibleRect)
    }

    override func drawBackground(forGlyphRange glyphsToShow: NSRange, at origin: NSPoint) {
        if isHighlightEnabled {
            drawCurrentLineHighlight(glyphsToShow: glyphsToShow, origin: origin)
        }
        super.drawBackground(forGlyphRange: glyphsToShow, at: origin)
    }

    private func drawCurrentLineHighlight(glyphsToShow: NSRange, origin: NSPoint) {
        guard let textView = highlightTextView,
              let textContainer = textContainers.first else { return }

        let string = textView.string as NSString
        let lineCharRange = currentLineCharacterRange()
        let selectedRange = textView.selectedRange()
        let fillRanges = subranges(of: lineCharRange, excluding: selectedRange)
        let rowColor = highlightColor.withAlphaComponent(0.35)
        let accentColor = highlightColor.blended(withFraction: 0.45, of: .black) ?? highlightColor

        if string.length == 0 {
            let lineHeight = textView.font?.boundingRectForFont.size.height ?? 16
            let inset = textView.textContainerInset
            let padding = textContainer.lineFragmentPadding
            let baseX = origin.x + inset.width - padding
            let baseY = origin.y + inset.height
            let rowRect = NSRect(x: baseX, y: baseY, width: textContainer.size.width, height: lineHeight)
            drawAccentBar(at: NSPoint(x: baseX, y: baseY), height: lineHeight, color: accentColor)
            rowColor.setFill()
            rowRect.fill()
            return
        }

        for fillRange in fillRanges {
            let lineGlyphRange = glyphRange(forCharacterRange: fillRange, actualCharacterRange: nil)
            let drawRange = NSIntersectionRange(lineGlyphRange, glyphsToShow)
            guard drawRange.length > 0 else { continue }

            enumerateLineFragments(forGlyphRange: drawRange) { lineRect, usedRect, _, _, _ in
                var fillRect = usedRect.isEmpty ? lineRect : usedRect
                fillRect.origin.x += origin.x
                fillRect.origin.y += origin.y
                rowColor.setFill()
                fillRect.fill()
            }
        }

        let accentGlyphRange = glyphRange(forCharacterRange: lineCharRange, actualCharacterRange: nil)
        let accentDrawRange = NSIntersectionRange(accentGlyphRange, glyphsToShow)
        guard accentDrawRange.length > 0 else { return }

        enumerateLineFragments(forGlyphRange: accentDrawRange) { lineRect, _, _, _, _ in
            let barOrigin = NSPoint(x: origin.x + lineRect.origin.x, y: origin.y + lineRect.origin.y)
            self.drawAccentBar(at: barOrigin, height: lineRect.height, color: accentColor)
        }
    }

    private func drawAccentBar(at origin: NSPoint, height: CGFloat, color: NSColor) {
        let barRect = NSRect(x: origin.x, y: origin.y, width: Self.accentWidth, height: height)
        color.setFill()
        barRect.fill()
    }

    private func subranges(of container: NSRange, excluding excluded: NSRange) -> [NSRange] {
        guard container.length > 0 else { return [] }
        let intersection = NSIntersectionRange(container, excluded)
        guard intersection.length > 0 else { return [container] }

        var ranges: [NSRange] = []
        if intersection.location > container.location {
            ranges.append(NSRange(
                location: container.location,
                length: intersection.location - container.location
            ))
        }

        let containerEnd = NSMaxRange(container)
        let intersectionEnd = NSMaxRange(intersection)
        if intersectionEnd < containerEnd {
            ranges.append(NSRange(location: intersectionEnd, length: containerEnd - intersectionEnd))
        }

        return ranges
    }
}