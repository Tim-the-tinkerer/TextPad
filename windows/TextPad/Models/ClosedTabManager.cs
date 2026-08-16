namespace TextPad.Models;

public sealed class ClosedTabSnapshot
{
    public required string Content { get; init; }
    public string? FilePath { get; init; }
    public string EncodingName { get; init; } = "UTF-8";
    public DocumentFormat Format { get; init; } = DocumentFormat.PlainText;
    public string? RtfDataBase64 { get; init; }
}

public sealed class ClosedTabManager
{
    private readonly Stack<ClosedTabSnapshot> _stack = new();
    private const int MaxEntries = 20;

    public void Push(ClosedTabSnapshot snapshot)
    {
        _stack.Push(snapshot);
        while (_stack.Count > MaxEntries)
        {
            var items = _stack.ToArray().Take(MaxEntries).Reverse().ToList();
            _stack.Clear();
            foreach (var item in items)
                _stack.Push(item);
        }
    }

    public ClosedTabSnapshot? Pop() => _stack.Count > 0 ? _stack.Pop() : null;

    public bool CanReopen => _stack.Count > 0;
}