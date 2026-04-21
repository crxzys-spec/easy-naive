using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.SingBox.Process;
using Xunit;

namespace EasyNaive.SingBox.Tests;

public sealed class ElevationSessionStoreTests : IDisposable
{
    private readonly List<string> _sessionPaths = new();

    [Fact]
    public void GetSummary_WhenFileIsMissing_ReturnsMissingSummary()
    {
        var sessionPath = CreateSessionPath();

        var summary = ElevationSessionStore.GetSummary(sessionPath);

        Assert.False(summary.Exists);
        Assert.False(summary.IsReadable);
        Assert.Equal(ElevationSessionStatus.Unknown, summary.Status);
        Assert.Contains("No elevation session file", summary.Detail);
    }

    [Fact]
    public void GetSummary_WhenFileIsCorrupt_ReturnsUnreadableSummary()
    {
        var sessionPath = CreateSessionPath();
        File.WriteAllText(sessionPath, "{not-json");

        var summary = ElevationSessionStore.GetSummary(sessionPath);

        Assert.True(summary.Exists);
        Assert.False(summary.IsReadable);
        Assert.Contains("could not be parsed", summary.Detail);
    }

    [Fact]
    public void WriteAndGetSummary_PreservesStoppedSessionDetails()
    {
        var sessionPath = CreateSessionPath();
        var updatedAt = new DateTimeOffset(2026, 4, 21, 10, 30, 0, TimeSpan.Zero);

        ElevationSessionStore.Write(sessionPath, new ElevationSessionState
        {
            Status = ElevationSessionStatus.Stopped,
            HelperProcessId = 1234,
            SingBoxProcessId = 5678,
            UpdatedAt = updatedAt,
            ExitCode = 0
        });

        var summary = ElevationSessionStore.GetSummary(sessionPath);

        Assert.True(summary.Exists);
        Assert.True(summary.IsReadable);
        Assert.Equal(ElevationSessionStatus.Stopped, summary.Status);
        Assert.Equal(1234, summary.HelperProcessId);
        Assert.Equal(5678, summary.SingBoxProcessId);
        Assert.False(summary.IsSingBoxProcessAlive);
        Assert.Equal(0, summary.ExitCode);
        Assert.Equal(updatedAt, summary.UpdatedAt);
        Assert.Contains("Status=Stopped", summary.Detail);
        Assert.Contains("HelperPID=1234", summary.Detail);
        Assert.Contains("SingBoxPID=5678", summary.Detail);
    }

    [Fact]
    public void MarkStopped_WhenSessionExists_UpdatesStatusAndExitCode()
    {
        var sessionPath = CreateSessionPath();
        ElevationSessionStore.Write(sessionPath, new ElevationSessionState
        {
            Status = ElevationSessionStatus.Running,
            HelperProcessId = 111,
            SingBoxProcessId = 222,
            UpdatedAt = DateTimeOffset.Now
        });

        ElevationSessionStore.MarkStopped(sessionPath, 9, "manual stop");

        var session = ElevationSessionStore.TryRead(sessionPath);

        Assert.NotNull(session);
        Assert.Equal(ElevationSessionStatus.Stopped, session.Status);
        Assert.Equal(9, session.ExitCode);
        Assert.Equal("manual stop", session.Error);
    }

    public void Dispose()
    {
        foreach (var sessionPath in _sessionPaths)
        {
            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
            }
        }
    }

    private string CreateSessionPath()
    {
        var sessionPath = Path.Combine(Path.GetTempPath(), $"EasyNaive.ElevationSessionStoreTests.{Guid.NewGuid():N}.json");
        _sessionPaths.Add(sessionPath);
        return sessionPath;
    }
}
