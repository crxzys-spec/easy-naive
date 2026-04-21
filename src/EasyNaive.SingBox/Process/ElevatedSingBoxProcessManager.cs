using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace EasyNaive.SingBox.Process;

public sealed class ElevatedSingBoxProcessManager : IDisposable
{
    private readonly object _syncRoot = new();
    private DiagnosticsProcess? _process;
    private string? _sessionPath;
    private CancellationTokenSource? _exitMonitorCancellationTokenSource;
    private Task? _exitMonitorTask;
    private bool _suppressExitEvent;

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

    public async Task StartAsync(SingBoxStartOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ElevationExecutablePath))
        {
            throw new InvalidOperationException("Elevation executable path is required for elevated startup.");
        }

        if (string.IsNullOrWhiteSpace(options.ElevationSessionPath))
        {
            throw new InvalidOperationException("Elevation session path is required for elevated startup.");
        }

        if (!File.Exists(options.ElevationExecutablePath))
        {
            throw new FileNotFoundException("Elevation helper executable was not found.", options.ElevationExecutablePath);
        }

        await StopAsync(options.ElevationSessionPath, cancellationToken);

        var sessionDirectory = Path.GetDirectoryName(options.ElevationSessionPath);
        if (!string.IsNullOrWhiteSpace(sessionDirectory))
        {
            Directory.CreateDirectory(sessionDirectory);
        }

        if (File.Exists(options.ElevationSessionPath))
        {
            File.Delete(options.ElevationSessionPath);
        }

        _sessionPath = options.ElevationSessionPath;

        var helperProcess = DiagnosticsProcess.Start(CreateHelperStartInfo(
            options.ElevationExecutablePath,
            BuildStartArguments(options)));

        if (helperProcess is null)
        {
            throw new InvalidOperationException("Failed to launch the elevation helper.");
        }

        using (helperProcess)
        {
            var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(20);

            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var session = ElevationSessionStore.TryRead(options.ElevationSessionPath);
                if (session is not null)
                {
                    if (session.Status == ElevationSessionStatus.Failed)
                    {
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(session.Error) ? "The elevation helper failed to start TUN mode." : session.Error);
                    }

                    if (session.Status == ElevationSessionStatus.Running && session.SingBoxProcessId > 0)
                    {
                        var process = TryAttachToProcess(session.SingBoxProcessId);
                        if (process is not null)
                        {
                            AttachProcess(process, options.ElevationSessionPath);
                            return;
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            }
        }

        throw new InvalidOperationException("The elevation helper did not report a running TUN session in time.");
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return StopAsync(null, cancellationToken);
    }

    public async Task StopAsync(string? sessionPath, CancellationToken cancellationToken = default)
    {
        string? effectiveSessionPath;
        DiagnosticsProcess? process;

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(sessionPath))
            {
                _sessionPath = sessionPath;
            }

            effectiveSessionPath = _sessionPath;
            process = _process;
            _suppressExitEvent = true;
        }

        await StopExitMonitorAsync();

        if (!string.IsNullOrWhiteSpace(effectiveSessionPath) && File.Exists(effectiveSessionPath))
        {
            var session = ElevationSessionStore.TryRead(effectiveSessionPath);
            if (session is not null)
            {
                await InvokeStopHelperAsync(effectiveSessionPath, cancellationToken);
            }
        }

        if (process is not null && TryIsRunning(process))
        {
            var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
            while (TryIsRunning(process) && DateTimeOffset.UtcNow < timeoutAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(effectiveSessionPath) && File.Exists(effectiveSessionPath))
        {
            ElevationSessionStore.MarkStopped(effectiveSessionPath);
        }

        lock (_syncRoot)
        {
            CleanupProcess();
            _sessionPath = null;
            _suppressExitEvent = false;
        }
    }

    public void Dispose()
    {
        StopExitMonitorAsync().GetAwaiter().GetResult();
        lock (_syncRoot)
        {
            CleanupProcess();
            _sessionPath = null;
        }
    }

    private void AttachProcess(DiagnosticsProcess process, string sessionPath)
    {
        lock (_syncRoot)
        {
            CleanupProcess();
            _process = process;
            _sessionPath = sessionPath;
            _suppressExitEvent = false;
        }

        StartExitMonitor(sessionPath);
    }

    private void CleanupProcess()
    {
        _process?.Dispose();
        _process = null;
    }

    private void StartExitMonitor(string sessionPath)
    {
        _exitMonitorCancellationTokenSource = new CancellationTokenSource();
        _exitMonitorTask = MonitorExitAsync(sessionPath, _exitMonitorCancellationTokenSource.Token);
    }

    private async Task StopExitMonitorAsync()
    {
        var cancellationTokenSource = _exitMonitorCancellationTokenSource;
        var monitorTask = _exitMonitorTask;

        _exitMonitorCancellationTokenSource = null;
        _exitMonitorTask = null;

        if (cancellationTokenSource is not null)
        {
            cancellationTokenSource.Cancel();
        }

        if (monitorTask is not null)
        {
            try
            {
                await monitorTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when intentionally stopping the monitor.
            }
        }

        cancellationTokenSource?.Dispose();
    }

    private async Task MonitorExitAsync(string sessionPath, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DiagnosticsProcess? process;
            bool suppressExitEvent;

            lock (_syncRoot)
            {
                process = _process;
                suppressExitEvent = _suppressExitEvent;
            }

            if (!TryIsRunning(process))
            {
                int? exitCode = TryGetExitCode(process);
                ElevationSessionStore.MarkStopped(sessionPath, exitCode);

                lock (_syncRoot)
                {
                    CleanupProcess();
                    _sessionPath = null;
                }

                if (!suppressExitEvent)
                {
                    Exited?.Invoke(this, exitCode);
                }

                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private static async Task InvokeStopHelperAsync(string sessionPath, CancellationToken cancellationToken)
    {
        var session = ElevationSessionStore.TryRead(sessionPath);
        if (session is null || string.IsNullOrWhiteSpace(session.ElevationExecutablePath))
        {
            return;
        }

        if (!File.Exists(session.ElevationExecutablePath))
        {
            return;
        }

        using var helperProcess = DiagnosticsProcess.Start(CreateHelperStartInfo(
            session.ElevationExecutablePath,
            BuildStopArguments(sessionPath)));

        if (helperProcess is null)
        {
            throw new InvalidOperationException("Failed to launch the elevation helper for shutdown.");
        }

        await helperProcess.WaitForExitAsync(cancellationToken);
        if (helperProcess.ExitCode != 0)
        {
            throw new InvalidOperationException($"The elevation helper failed to stop TUN mode. Exit code: {helperProcess.ExitCode}.");
        }
    }

    private static DiagnosticsProcessStartInfo CreateHelperStartInfo(string elevationExecutablePath, string arguments)
    {
        return new DiagnosticsProcessStartInfo
        {
            FileName = elevationExecutablePath,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(elevationExecutablePath) ?? Environment.CurrentDirectory
        };
    }

    private static string BuildStartArguments(SingBoxStartOptions options)
    {
        return string.Join(" ",
            "start",
            Quote(options.ElevationSessionPath!),
            Quote(options.ExecutablePath),
            Quote(options.ConfigPath),
            Quote(options.WorkingDirectory),
            Quote(options.LogPath));
    }

    private static string BuildStopArguments(string sessionPath)
    {
        return string.Join(" ", "stop", Quote(sessionPath));
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static DiagnosticsProcess? TryAttachToProcess(int processId)
    {
        try
        {
            var process = DiagnosticsProcess.GetProcessById(processId);
            if (TryIsRunning(process))
            {
                return process;
            }

            process.Dispose();
            return null;
        }
        catch
        {
            return null;
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
        catch
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
        catch
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
        catch
        {
            return null;
        }
    }
}
