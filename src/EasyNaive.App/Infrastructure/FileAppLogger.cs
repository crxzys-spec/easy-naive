using EasyNaive.Core.Logging;

namespace EasyNaive.App.Infrastructure;

internal sealed class FileAppLogger : IDisposable
{
    private readonly object _syncRoot = new();
    private RotatingLogFileWriter? _writer;

    public FileAppLogger(string path)
    {
        _writer = new RotatingLogFileWriter(path);
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(string level, string message)
    {
        lock (_syncRoot)
        {
            _writer?.WriteLine($"[{DateTimeOffset.Now:u}] [{level}] {message}");
        }
    }
}
