using System.Text;
using DiagnosticsDataReceivedEventArgs = System.Diagnostics.DataReceivedEventArgs;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace EasyNaive.SingBox.Process;

public sealed class SingBoxProcessManager : IDisposable
{
    private readonly object _syncRoot = new();
    private DiagnosticsProcess? _process;
    private StreamWriter? _logWriter;
    private int? _lastExitCode;

    public event EventHandler<int?>? Exited;

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return TryIsRunning(_process);
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_syncRoot)
            {
                return TryGetProcessId(_process);
            }
        }
    }

    public async Task CheckConfigAsync(string executablePath, string configPath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("sing-box executable was not found.", executablePath);
        }

        var output = new StringBuilder();
        using var process = new DiagnosticsProcess
        {
            StartInfo = CreateCheckStartInfo(executablePath, configPath, workingDirectory),
            EnableRaisingEvents = false
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                output.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                output.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start sing-box config validation.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            return;
        }

        var detail = output.ToString().Trim();
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(detail)
                ? $"sing-box check failed with exit code {process.ExitCode}."
                : $"sing-box check failed: {detail}");
    }

    public async Task StartAsync(SingBoxStartOptions options, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.ExecutablePath))
        {
            throw new FileNotFoundException("sing-box executable was not found.", options.ExecutablePath);
        }

        Directory.CreateDirectory(options.WorkingDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(options.LogPath)!);

        await StopAsync(cancellationToken);
        CleanupOrphanProcesses(options.ExecutablePath);

        var process = new DiagnosticsProcess
        {
            StartInfo = CreateStartInfo(options),
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += OnProcessOutputDataReceived;
        process.ErrorDataReceived += OnProcessOutputDataReceived;
        process.Exited += OnProcessExited;

        lock (_syncRoot)
        {
            _lastExitCode = null;
            _logWriter = CreateLogWriter(options.LogPath);
        }

        WriteLogLine($"[{DateTimeOffset.Now:u}] Starting sing-box with config: {options.ConfigPath}");

        if (!process.Start())
        {
            CleanupProcess(process);
            throw new InvalidOperationException("Failed to start sing-box process.");
        }

        lock (_syncRoot)
        {
            _process = process;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        if (!IsRunning)
        {
            var exitCode = TryGetExitCode(process) ?? LastExitCode;
            CleanupProcess(process);
            throw new InvalidOperationException($"sing-box exited unexpectedly during startup. Exit code: {exitCode?.ToString() ?? "unknown"}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        DiagnosticsProcess? process;

        lock (_syncRoot)
        {
            process = _process;
        }

        if (process is null)
        {
            return;
        }

        WriteLogLine($"[{DateTimeOffset.Now:u}] Stopping sing-box.");

        if (TryIsRunning(process))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The caller decides how to surface cancellation. The process may already be exiting.
            }
            catch (InvalidOperationException)
            {
                // The process exited before we could stop it.
            }
        }

        CleanupProcess(process);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _process?.Dispose();
            _process = null;

            _logWriter?.Dispose();
            _logWriter = null;
        }
    }

    private int? LastExitCode
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastExitCode;
            }
        }
    }

    private static DiagnosticsProcessStartInfo CreateStartInfo(SingBoxStartOptions options)
    {
        var startInfo = new DiagnosticsProcessStartInfo
        {
            FileName = options.ExecutablePath,
            WorkingDirectory = options.WorkingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(options.ConfigPath);

        return startInfo;
    }

    private static DiagnosticsProcessStartInfo CreateCheckStartInfo(string executablePath, string configPath, string workingDirectory)
    {
        var startInfo = new DiagnosticsProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("check");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configPath);

        return startInfo;
    }

    private static StreamWriter CreateLogWriter(string logPath)
    {
        return new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    private void OnProcessOutputDataReceived(object sender, DiagnosticsDataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            WriteLogLine(e.Data);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var process = sender as DiagnosticsProcess;
        var exitCode = TryGetExitCode(process);

        lock (_syncRoot)
        {
            _lastExitCode = exitCode;
        }

        WriteLogLine($"[{DateTimeOffset.Now:u}] sing-box exited. Exit code: {exitCode?.ToString() ?? "unknown"}");
        CleanupProcess(process);
        Exited?.Invoke(this, exitCode);
    }

    private void CleanupProcess(DiagnosticsProcess? process)
    {
        lock (_syncRoot)
        {
            if (process is not null)
            {
                process.OutputDataReceived -= OnProcessOutputDataReceived;
                process.ErrorDataReceived -= OnProcessOutputDataReceived;
                process.Exited -= OnProcessExited;

                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                }

                try
                {
                    process.Dispose();
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }

            _logWriter?.Dispose();
            _logWriter = null;
        }
    }

    private void WriteLogLine(string message)
    {
        lock (_syncRoot)
        {
            _logWriter?.WriteLine(message);
        }
    }

    private static bool TryIsRunning(DiagnosticsProcess? process)
    {
        if (process is null)
        {
            return false;
        }

        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static int? TryGetProcessId(DiagnosticsProcess? process)
    {
        if (!TryIsRunning(process))
        {
            return null;
        }

        try
        {
            return process!.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static int? TryGetExitCode(DiagnosticsProcess? process)
    {
        if (process is null)
        {
            return null;
        }

        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void CleanupOrphanProcesses(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        foreach (var candidate in DiagnosticsProcess.GetProcessesByName(processName))
        {
            try
            {
                string? candidatePath = null;
                try
                {
                    candidatePath = candidate.MainModule?.FileName;
                }
                catch
                {
                    // Ignore processes we cannot inspect.
                }

                if (!string.Equals(candidatePath, executablePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!candidate.HasExited)
                {
                    candidate.Kill(entireProcessTree: true);
                    candidate.WaitForExit(5000);
                }
            }
            catch
            {
                // Best effort orphan cleanup only.
            }
            finally
            {
                candidate.Dispose();
            }
        }
    }
}
