using System.ServiceProcess;
using EasyNaive.SingBox.Service;

namespace EasyNaive.Service;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--console", StringComparison.OrdinalIgnoreCase)) ||
            Environment.UserInteractive)
        {
            await RunConsoleAsync();
            return 0;
        }

        ServiceBase.Run(new EasyNaiveWindowsService());
        return 0;
    }

    private static async Task RunConsoleAsync()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var runtime = new SingBoxServiceRuntime();
        var server = new SingBoxNamedPipeServer(runtime);
        var runTask = server.RunAsync(cancellationTokenSource.Token);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        await runTask;
        await runtime.StopAsync(string.Empty, CancellationToken.None);
    }
}

internal sealed class EasyNaiveWindowsService : ServiceBase
{
    private readonly SingBoxServiceRuntime _runtime = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;

    public EasyNaiveWindowsService()
    {
        ServiceName = SingBoxServiceProtocol.ServiceName;
        CanStop = true;
        CanShutdown = true;
    }

    protected override void OnStart(string[] args)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _serverTask = new SingBoxNamedPipeServer(_runtime).RunAsync(_cancellationTokenSource.Token);
    }

    protected override void OnStop()
    {
        StopServiceWork();
    }

    protected override void OnShutdown()
    {
        StopServiceWork();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopServiceWork();
            _runtime.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void StopServiceWork()
    {
        _cancellationTokenSource?.Cancel();

        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is OperationCanceledException))
        {
            // Expected when stopping the service.
        }

        _runtime.StopAsync(string.Empty, CancellationToken.None).GetAwaiter().GetResult();
    }
}
