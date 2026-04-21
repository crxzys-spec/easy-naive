namespace EasyNaive.Platform.Windows.Instance;

public sealed class WindowsSingleInstanceGuard : IDisposable
{
    private const string DefaultMutexName = @"Local\EasyNaiveTray.Singleton";

    private readonly Mutex _mutex;

    public WindowsSingleInstanceGuard(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var isPrimaryInstance);
        IsPrimaryInstance = isPrimaryInstance;
    }

    public bool IsPrimaryInstance { get; }

    public static WindowsSingleInstanceGuard CreateDefault() => new(DefaultMutexName);

    public void Dispose()
    {
        _mutex.Dispose();
    }
}
