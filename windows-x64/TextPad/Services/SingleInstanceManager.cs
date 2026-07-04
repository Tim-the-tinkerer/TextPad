using System.IO;
using System.IO.Pipes;
using System.Text;

namespace TextPad.Services;

public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = "TextPad.Editor.SingleInstance.v1";
    private const string PipeName = "TextPad.Editor.IPC.v1";
    private const string OpenCommand = "OPEN";
    private const string ActivateCommand = "ACTIVATE";

    private readonly Mutex _mutex;
    private readonly bool _isPrimary;
    private CancellationTokenSource? _listenCts;

    private SingleInstanceManager(Mutex mutex, bool isPrimary)
    {
        _mutex = mutex;
        _isPrimary = isPrimary;
    }

    public static SingleInstanceManager Acquire()
    {
        var mutex = new Mutex(true, MutexName, out var createdNew);
        return new SingleInstanceManager(mutex, createdNew);
    }

    public bool IsPrimary => _isPrimary;

    public static bool TryForwardToRunningInstance(IReadOnlyList<string> filePaths)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };

            if (filePaths.Count > 0)
            {
                writer.WriteLine(OpenCommand);
                foreach (var path in filePaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                        writer.WriteLine(path);
                }
            }
            else
            {
                writer.WriteLine(ActivateCommand);
            }

            writer.WriteLine();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StartListening(Action<IReadOnlyList<string>> onOpenFiles, Action onActivate)
    {
        if (!_isPrimary)
            return;

        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        _ = Task.Run(() => ListenLoop(onOpenFiles, onActivate, token), token);
    }

    private static async Task ListenLoop(
        Action<IReadOnlyList<string>> onOpenFiles,
        Action onActivate,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);
                var message = await ReadMessageAsync(server, token);
                if (message is null)
                    continue;

                if (message.Command == OpenCommand)
                    onOpenFiles(message.Paths);
                else if (message.Command == ActivateCommand)
                    onActivate();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(100, token);
            }
        }
    }

    private static async Task<PipeMessage?> ReadMessageAsync(NamedPipeServerStream server, CancellationToken token)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var command = await reader.ReadLineAsync(token);
        if (string.IsNullOrEmpty(command))
            return null;

        var paths = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null || line.Length == 0)
                break;
            paths.Add(line);
        }

        return new PipeMessage(command, paths);
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();
        if (_isPrimary)
        {
            try { _mutex.ReleaseMutex(); } catch { /* already released */ }
        }
        _mutex.Dispose();
    }

    private sealed record PipeMessage(string Command, IReadOnlyList<string> Paths);
}