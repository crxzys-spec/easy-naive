using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.SingBox.Service;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace EasyNaive.SingBox.Process;

public sealed class ElevatedSingBoxProcessManager : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly SingBoxServiceClient _serviceClient;
    private DiagnosticsProcess? _process;
    private string? _sessionPath;
    private int? _serviceProcessId;
    private CancellationTokenSource? _exitMonitorCancellationTokenSource;
    private Task? _exitMonitorTask;
    private bool _suppressExitEvent;
    private bool _startedByService;

    public ElevatedSingBoxProcessManager()
        : this(new SingBoxServiceClient())
    {
    }

    internal ElevatedSingBoxProcessManager(SingBoxServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
    }

    public event EventHandler<int?>? Exited;

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                if (_startedByService)
                {
                    return _serviceProcessId is > 0;
                }

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
                if (_startedByService)
                {
                    return _serviceProcessId;
                }

                return TryGetProcessId(_process);
            }
        }
    }

    public async Task StartAsync(SingBoxStartOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ElevationSessionPath))
        {
            throw new InvalidOperationException("Elevation session path is required for elevated startup.");
        }

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

        if (await TryStartViaServiceAsync(options, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ElevationExecutablePath))
        {
            throw new InvalidOperationException("Elevation executable path is required for elevated startup.");
        }

        if (!File.Exists(options.ElevationExecutablePath))
        {
            throw new FileNotFoundException("Elevation helper executable was not found.", options.ElevationExecutablePath);
        }

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
                            AttachProcess(process, options.ElevationSessionPath, startedByService: false);
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
            var stoppedByService = await TryStopViaServiceAsync(effectiveSessionPath, cancellationToken);
            if (stoppedByService)
            {
                if (process is not null && TryIsRunning(process))
                {
                    var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
                    while (TryIsRunning(process) && DateTimeOffset.UtcNow < timeoutAt)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                    }
                }

                if (File.Exists(effectiveSessionPath))
                {
                    ElevationSessionStore.MarkStopped(effectiveSessionPath);
                }

                lock (_syncRoot)
                {
                    CleanupProcess();
                    _sessionPath = null;
                    _serviceProcessId = null;
                    _startedByService = false;
                    _suppressExitEvent = false;
                }

                return;
            }

            var session = ElevationSessionStore.TryRead(effectiveSessionPath);
            if (session is not null && ShouldInvokeStopHelper(session))
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
            _serviceProcessId = null;
            _startedByService = false;
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
            _serviceProcessId = null;
            _startedByService = false;
        }
    }

    private void AttachProcess(DiagnosticsProcess process, string sessionPath, bool startedByService)
    {
        lock (_syncRoot)
        {
            CleanupProcess();
            _process = process;
            _sessionPath = sessionPath;
            _serviceProcessId = null;
            _startedByService = startedByService;
            _suppressExitEvent = false;
        }

        StartExitMonitor(sessionPath);
    }

    private void AttachServiceSession(int processId, string sessionPath)
    {
        lock (_syncRoot)
        {
            CleanupProcess();
            _serviceProcessId = processId;
            _sessionPath = sessionPath;
            _startedByService = true;
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
            bool startedByService;
            int? serviceProcessId;

            lock (_syncRoot)
            {
                process = _process;
                suppressExitEvent = _suppressExitEvent;
                startedByService = _startedByService;
                serviceProcessId = _serviceProcessId;
            }

            if (startedByService)
            {
                var serviceStatus = await _serviceClient.GetStatusAsync(cancellationToken);
                if (serviceStatus is null ||
                    serviceStatus.Success &&
                    string.Equals(serviceStatus.Status, ElevationSessionStatus.Running.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    (serviceStatus.ProcessId is null || serviceStatus.ProcessId == serviceProcessId))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }

                ElevationSessionStore.MarkStopped(sessionPath, serviceStatus.ExitCode);

                lock (_syncRoot)
                {
                    CleanupProcess();
                    _sessionPath = null;
                    _serviceProcessId = null;
                    _startedByService = false;
                }

                if (!suppressExitEvent)
                {
                    Exited?.Invoke(this, serviceStatus.ExitCode);
                }

                return;
            }

            if (!TryIsRunning(process))
            {
                int? exitCode = TryGetExitCode(process);
                ElevationSessionStore.MarkStopped(sessionPath, exitCode);

                lock (_syncRoot)
                {
                    CleanupProcess();
                    _sessionPath = null;
                    _serviceProcessId = null;
                    _startedByService = false;
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

    private async Task<bool> TryStartViaServiceAsync(SingBoxStartOptions options, CancellationToken cancellationToken)
    {
        var response = await _serviceClient.StartAsync(
            options.ExecutablePath,
            options.ConfigPath,
            options.WorkingDirectory,
            options.LogPath,
            options.ElevationSessionPath!,
            cancellationToken);

        if (response is null)
        {
            return false;
        }

        if (!response.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? "EasyNaive service failed to start TUN mode."
                    : response.Message);
        }

        if (response.ProcessId is not int processId || processId <= 0)
        {
            throw new InvalidOperationException("EasyNaive service did not return a sing-box process id.");
        }

        AttachServiceSession(processId, options.ElevationSessionPath!);
        return true;
    }

    private async Task<bool> TryStopViaServiceAsync(string sessionPath, CancellationToken cancellationToken)
    {
        bool shouldTryService;

        lock (_syncRoot)
        {
            shouldTryService = _startedByService;
        }

        var session = ElevationSessionStore.TryRead(sessionPath);
        if (!shouldTryService && session is not null)
        {
            var executableName = Path.GetFileName(session.ElevationExecutablePath);
            shouldTryService = string.Equals(executableName, "EasyNaive.Service.exe", StringComparison.OrdinalIgnoreCase);
        }

        if (!shouldTryService)
        {
            return false;
        }

        var response = await _serviceClient.StopAsync(sessionPath, cancellationToken);
        if (response is null)
        {
            return false;
        }

        if (!response.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? "EasyNaive service failed to stop TUN mode."
                    : response.Message);
        }

        return true;
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

    private static bool ShouldInvokeStopHelper(ElevationSessionState session)
    {
        if (session.Status != ElevationSessionStatus.Running)
        {
            return false;
        }

        return IsProcessAlive(session.SingBoxProcessId) || IsProcessAlive(session.HelperProcessId);
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

    private static bool IsProcessAlive(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = DiagnosticsProcess.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
