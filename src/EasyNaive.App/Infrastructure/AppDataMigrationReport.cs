namespace EasyNaive.App.Infrastructure;

internal sealed class AppDataMigrationReport
{
    private readonly List<Entry> _entries = new();

    public int CopiedFileCount { get; private set; }

    public int FailedFileCount { get; private set; }

    public void AddCopied(string sourcePath, string targetPath)
    {
        CopiedFileCount++;
        _entries.Add(new Entry(false, $"Migrated legacy file '{sourcePath}' -> '{targetPath}'."));
    }

    public void AddFailed(string sourcePath, string targetPath, Exception exception)
    {
        FailedFileCount++;
        _entries.Add(new Entry(true, $"Failed to migrate legacy file '{sourcePath}' -> '{targetPath}': {exception.Message}"));
    }

    public void WriteTo(FileAppLogger logger)
    {
        if (CopiedFileCount == 0 && FailedFileCount == 0)
        {
            return;
        }

        logger.Info($"Legacy app data migration finished. Copied={CopiedFileCount}, Failed={FailedFileCount}.");

        foreach (var entry in _entries)
        {
            if (entry.IsError)
            {
                logger.Error(entry.Message);
            }
            else
            {
                logger.Info(entry.Message);
            }
        }
    }

    private readonly record struct Entry(bool IsError, string Message);
}
