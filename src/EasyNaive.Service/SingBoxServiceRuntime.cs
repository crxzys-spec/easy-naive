using System.Diagnostics;
using System.Text;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.SingBox.Process;
using EasyNaive.SingBox.Service;

namespace EasyNaive.Service;

internal sealed class SingBoxServiceRuntime : IDisposable
{
    private static readonly TimeSpan StartupExitProbeDelay = TimeSpan.FromMilliseconds(300);
    private readonly object _syncRoot = new();
    private Process? _process;
    private StreamWriter? _logWriter;
    private string _sessionPath = string.Empty;
    private int? _lastExitCode;
    private bool _stopping;

    public async Task<SingBoxServiceResponse> StartAsync(SingBoxServiceRequest request, CancellationToken cancellationToken)
    {
        ValidateStartRequest(request);

        await StopAsync(request.SessionPath, cancellationToken);
        CleanupOrphanProcesses(request.ExecutablePath);

        Directory.CreateDirectory(request.WorkingDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(request.LogPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(request.SessionPath)!);

        var process = CreateSingBoxProcess(request);
        var logWriter = CreateLogWriter(request.LogPath);
        process.OutputDataReceived += OnProcessOutputDataReceived;
        process.ErrorDataReceived += OnProcessOutputDataReceived;
        process.Exited += OnProcessExited;

        lock (_syncRoot)
        {
            _process = process;
            _logWriter = logWriter;
            _sessionPath = request.SessionPath;
            _lastExitCode = null;
            _stopping = false;
        }

        WriteLogLine($"[{DateTimeOffset.Now:u}] [service] Starting sing-box with config: {request.ConfigPath}");

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start sing-box from EasyNaive service.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            WriteSession(request, ElevationSessionStatus.Running, process.Id);

            await Task.Delay(StartupExitProbeDelay, cancellationToken);

            if (!TryIsRunning(process))
            {
                var exitCode = TryGetExitCode(process) ?? LastExitCode;
                WriteSession(request, ElevationSessionStatus.Failed, process.Id, exitCode, $"sing-box exited during service startup. Exit code: {exitCode?.ToString() ?? "unknown"}");
                CleanupProcess(process);

                return new SingBoxServiceResponse
                {
                    Success = false,
                    Status = ElevationSessionStatus.Failed.ToString(),
                    ExitCode = exitCode,
                    Message = $"sing-box exited during service startup. Exit code: {exitCode?.ToString() ?? "unknown"}"
                };
            }

            return new SingBoxServiceResponse
            {
                Success = true,
                Status = ElevationSessionStatus.Running.ToString(),
                ProcessId = process.Id,
                Message = "sing-box started by EasyNaive service."
            };
        }
        catch (Exception ex)
        {
            WriteSession(request, ElevationSessionStatus.Failed, TryGetProcessId(process) ?? 0, TryGetExitCode(process), ex.Message);
            WriteLogLine($"[{DateTimeOffset.Now:u}] [service] Startup failed: {ex}");
            CleanupProcess(process);
            return new SingBoxServiceResponse
            {
                Success = false,
                Status = ElevationSessionStatus.Failed.ToString(),
                Message = ex.Message
            };
        }
    }

    public async Task<SingBoxServiceResponse> StopAsync(string sessionPath, CancellationToken cancellationToken)
    {
        Process? process;
        string effectiveSessionPath;

        lock (_syncRoot)
        {
            _stopping = true;
            process = _process;
            effectiveSessionPath = string.IsNullOrWhiteSpace(sessionPath) ? _sessionPath : sessionPath;
        }

        WriteLogLine($"[{DateTimeOffset.Now:u}] [service] Stop requested.");

        if (process is not null)
        {
            var processId = TryGetProcessId(process);
            if (processId is > 0)
            {
                TryKillProcess(processId.Value);
            }

            await WaitForExitAsync(process, TimeSpan.FromSeconds(10), cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(effectiveSessionPath))
        {
            var session = ElevationSessionStore.TryRead(effectiveSessionPath);
            if (session is not null && session.SingBoxProcessId > 0)
            {
                TryKillProcess(session.SingBoxProcessId);
            }
        }

        if (!string.IsNullOrWhiteSpace(effectiveSessionPath) && File.Exists(effectiveSessionPath))
        {
            ElevationSessionStore.MarkStopped(effectiveSessionPath);
        }

        CleanupProcess(process);

        lock (_syncRoot)
        {
            _sessionPath = string.Empty;
            _stopping = false;
        }

        return new SingBoxServiceResponse
        {
            Success = true,
            Status = ElevationSessionStatus.Stopped.ToString(),
            Message = "sing-box stopped by EasyNaive service."
        };
    }

    public SingBoxServiceResponse GetStatus()
    {
        lock (_syncRoot)
        {
            return new SingBoxServiceResponse
            {
                Success = true,
                Status = TryIsRunning(_process) ? ElevationSessionStatus.Running.ToString() : ElevationSessionStatus.Stopped.ToString(),
                ProcessId = TryGetProcessId(_process),
                ExitCode = _lastExitCode
            };
        }
    }

    public void Dispose()
    {
        StopAsync(string.Empty, CancellationToken.None).GetAwaiter().GetResult();
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

    private static void ValidateStartRequest(SingBoxServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath) || !File.Exists(request.ExecutablePath))
        {
            throw new FileNotFoundException("sing-box executable was not found.", request.ExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(request.ConfigPath) || !File.Exists(request.ConfigPath))
        {
            throw new FileNotFoundException("sing-box config was not found.", request.ConfigPath);
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new InvalidOperationException("sing-box working directory is required.");
        }

        if (string.IsNullOrWhiteSpace(request.LogPath))
        {
            throw new InvalidOperationException("sing-box log path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SessionPath))
        {
            throw new InvalidOperationException("EasyNaive elevation session path is required.");
        }
    }

    private static Process CreateSingBoxProcess(SingBoxServiceRequest request)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                WorkingDirectory = request.WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(request.ConfigPath);

        return process;
    }

    private void WriteSession(
        SingBoxServiceRequest request,
        ElevationSessionStatus status,
        int singBoxProcessId,
        int? exitCode = null,
        string error = "")
    {
        ElevationSessionStore.Write(request.SessionPath, new ElevationSessionState
        {
            Status = status,
            HelperProcessId = Environment.ProcessId,
            SingBoxProcessId = singBoxProcessId,
            ConfigPath = request.ConfigPath,
            WorkingDirectory = request.WorkingDirectory,
            LogPath = request.LogPath,
            ElevationExecutablePath = Environment.ProcessPath ?? string.Empty,
            UpdatedAt = DateTimeOffset.Now,
            Error = error,
            ExitCode = exitCode
        });
    }

    private static StreamWriter CreateLogWriter(string logPath)
    {
        return new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    private void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            WriteLogLine(e.Data);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var process = sender as Process;
        var exitCode = TryGetExitCode(process);
        string sessionPath;
        bool stopping;

        lock (_syncRoot)
        {
            _lastExitCode = exitCode;
            sessionPath = _sessionPath;
            stopping = _stopping;
        }

        WriteLogLine($"[{DateTimeOffset.Now:u}] [service] sing-box exited. Exit code: {exitCode?.ToString() ?? "unknown"}");

        if (!string.IsNullOrWhiteSpace(sessionPath) && File.Exists(sessionPath))
        {
            ElevationSessionStore.MarkStopped(sessionPath, exitCode, stopping ? string.Empty : "sing-box exited unexpectedly.");
        }

        CleanupProcess(process);
    }

    private void CleanupProcess(Process? process)
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

    private static async Task WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Stop remains best effort. The next start request will clean up again.
        }
        catch (InvalidOperationException)
        {
            // The process may have already been detached by the Exited handler.
        }
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process may already have exited or access may be denied.
        }
    }

    private static void CleanupOrphanProcesses(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        foreach (var candidate in Process.GetProcessesByName(processName))
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

    private static bool TryIsRunning(Process? process)
    {
        if (process is null)
        {
            return false;
        }

        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int? TryGetProcessId(Process? process)
    {
        if (!TryIsRunning(process))
        {
            return null;
        }

        try
        {
            return process!.Id;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetExitCode(Process? process)
    {
        if (process is null)
        {
            return null;
        }

        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }
}
