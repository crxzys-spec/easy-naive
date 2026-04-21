namespace EasyNaive.SingBox.Process;

public sealed class SingBoxProcessOrchestrator : IDisposable
{
    private readonly SingBoxProcessManager _directManager;
    private readonly ElevatedSingBoxProcessManager _elevatedManager;

    public SingBoxProcessOrchestrator(
        SingBoxProcessManager directManager,
        ElevatedSingBoxProcessManager elevatedManager)
    {
        _directManager = directManager;
        _elevatedManager = elevatedManager;
        _directManager.Exited += OnManagerExited;
        _elevatedManager.Exited += OnManagerExited;
    }

    public event EventHandler<int?>? Exited;

    public bool IsRunning => _directManager.IsRunning || _elevatedManager.IsRunning;

    public int? ProcessId => _elevatedManager.ProcessId ?? _directManager.ProcessId;

    public Task CheckConfigAsync(string executablePath, string configPath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        return _directManager.CheckConfigAsync(executablePath, configPath, workingDirectory, cancellationToken);
    }

    public async Task StartAsync(SingBoxStartOptions options, CancellationToken cancellationToken = default)
    {
        await _elevatedManager.StopAsync(options.ElevationSessionPath, cancellationToken);
        await _directManager.StopAsync(cancellationToken);

        if (options.RequiresElevation)
        {
            await _elevatedManager.StartAsync(options, cancellationToken);
            return;
        }

        await _directManager.StartAsync(options, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _elevatedManager.StopAsync(cancellationToken);
        await _directManager.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        _directManager.Exited -= OnManagerExited;
        _elevatedManager.Exited -= OnManagerExited;
        _elevatedManager.Dispose();
        _directManager.Dispose();
    }

    private void OnManagerExited(object? sender, int? exitCode)
    {
        Exited?.Invoke(this, exitCode);
    }
}
