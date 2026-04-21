namespace EasyNaive.Core.Logging;

public sealed class RotatingLogFileWriter : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly int _retainedFiles;
    private StreamWriter? _writer;

    public RotatingLogFileWriter(
        string path,
        long maxBytes = LogFileRotator.DefaultMaxBytes,
        int retainedFiles = LogFileRotator.DefaultRetainedFiles)
    {
        _path = path;
        _maxBytes = maxBytes;
        _retainedFiles = retainedFiles;
        _writer = OpenWriter();
    }

    public void WriteLine(string message)
    {
        lock (_syncRoot)
        {
            EnsureWriter();
            _writer?.WriteLine(message);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void EnsureWriter()
    {
        if (_writer is not null && _writer.BaseStream.Length < _maxBytes)
        {
            return;
        }

        _writer?.Dispose();
        _writer = OpenWriter();
    }

    private StreamWriter OpenWriter()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        LogFileRotator.RotateIfNeeded(_path, _maxBytes, _retainedFiles);

        return new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            AutoFlush = true
        };
    }
}
