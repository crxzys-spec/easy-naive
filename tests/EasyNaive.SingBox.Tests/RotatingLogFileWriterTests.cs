using EasyNaive.Core.Logging;
using Xunit;

namespace EasyNaive.SingBox.Tests;

public sealed class RotatingLogFileWriterTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "EasyNaiveTests",
        nameof(RotatingLogFileWriterTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteLine_RotatesWhenCurrentFileReachesLimit()
    {
        Directory.CreateDirectory(_testDirectory);
        var logPath = Path.Combine(_testDirectory, "app.log");

        using (var writer = new RotatingLogFileWriter(logPath, maxBytes: 20, retainedFiles: 2))
        {
            writer.WriteLine(new string('a', 25));
            writer.WriteLine("after-rotate");
        }

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists($"{logPath}.1"));
        Assert.Contains("after-rotate", File.ReadAllText(logPath));
        Assert.Contains(new string('a', 25), File.ReadAllText($"{logPath}.1"));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_testDirectory))
        {
            return;
        }

        foreach (var path in Directory.GetFiles(_testDirectory))
        {
            File.Delete(path);
        }

        Directory.Delete(_testDirectory);
    }
}
