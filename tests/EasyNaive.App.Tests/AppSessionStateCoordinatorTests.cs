using EasyNaive.App.Session;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using Xunit;

namespace EasyNaive.App.Tests;

public sealed class AppSessionStateCoordinatorTests
{
    [Fact]
    public void BeginLaunch_CapturesPreviousSessionAndMarksRunning()
    {
        var state = new AppSessionState
        {
            LastShutdownWasClean = false,
            LastExitReason = SessionExitReason.UnexpectedTermination,
            LastRecoveryError = "previous failure"
        };
        var now = new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero);

        var info = AppSessionStateCoordinator.BeginLaunch(state, now);

        Assert.True(info.HadUncleanShutdown);
        Assert.Equal(SessionExitReason.UnexpectedTermination, info.PreviousExitReason);
        Assert.Equal("previous failure", info.PreviousRecoveryError);
        Assert.Equal(now, state.LastLaunchTime);
        Assert.False(state.LastShutdownWasClean);
        Assert.Equal(SessionExitReason.Running, state.LastExitReason);
    }

    [Theory]
    [InlineData(CoreStatus.Running, false, true, SessionExitReason.ApplicationExitConnected)]
    [InlineData(CoreStatus.Starting, false, true, SessionExitReason.ApplicationExitConnected)]
    [InlineData(CoreStatus.Stopped, true, true, SessionExitReason.ApplicationExitConnected)]
    [InlineData(CoreStatus.Stopped, false, false, SessionExitReason.ApplicationExitDisconnected)]
    public void ExitDecision_ReflectsConnectionAndProcessState(
        CoreStatus coreStatus,
        bool isProcessRunning,
        bool expectedPreserveRestore,
        SessionExitReason expectedExitReason)
    {
        Assert.Equal(expectedPreserveRestore, AppSessionStateCoordinator.ShouldPreserveRestoreConnection(coreStatus, isProcessRunning));
        Assert.Equal(expectedExitReason, AppSessionStateCoordinator.DetermineApplicationExitReason(coreStatus, isProcessRunning));
    }

    [Fact]
    public void MarkStartupRecoveryFailed_StoresFailureState()
    {
        var state = new AppSessionState
        {
            RestoreConnectionOnLaunch = true
        };
        var now = new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.Zero);

        AppSessionStateCoordinator.MarkStartupRecoveryFailed(state, "connect failed", now);

        Assert.False(state.RestoreConnectionOnLaunch);
        Assert.Equal(SessionExitReason.RecoveryFailed, state.LastExitReason);
        Assert.Equal(now, state.LastRecoveryAttemptTime);
        Assert.Equal("connect failed", state.LastRecoveryError);
    }

    [Fact]
    public void MarkStartupRecoverySucceeded_ClearsPreviousError()
    {
        var state = new AppSessionState
        {
            LastRecoveryError = "old error"
        };
        var now = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);

        AppSessionStateCoordinator.MarkStartupRecoverySucceeded(state, now);

        Assert.Equal(now, state.LastRecoveryAttemptTime);
        Assert.Equal(string.Empty, state.LastRecoveryError);
    }

    [Theory]
    [InlineData(true, SessionExitReason.ApplicationExitConnected)]
    [InlineData(false, SessionExitReason.ManualDisconnect)]
    public void MarkGracefulStop_UpdatesRestoreAndExitReason(bool preserveRestore, SessionExitReason expectedReason)
    {
        var state = new AppSessionState();

        AppSessionStateCoordinator.MarkGracefulStop(state, preserveRestore);

        Assert.Equal(preserveRestore, state.RestoreConnectionOnLaunch);
        Assert.Equal(expectedReason, state.LastExitReason);
    }

    [Fact]
    public void MarkUnexpectedTermination_EnablesRestoreAndSetsReason()
    {
        var state = new AppSessionState();

        AppSessionStateCoordinator.MarkUnexpectedTermination(state);

        Assert.True(state.RestoreConnectionOnLaunch);
        Assert.Equal(SessionExitReason.UnexpectedTermination, state.LastExitReason);
    }

    [Fact]
    public void MarkDisposed_MarksCleanShutdown()
    {
        var state = new AppSessionState
        {
            LastShutdownWasClean = false
        };
        var now = new DateTimeOffset(2026, 4, 21, 13, 0, 0, TimeSpan.Zero);

        AppSessionStateCoordinator.MarkDisposed(state, SessionExitReason.ApplicationExitConnected, now);

        Assert.True(state.LastShutdownWasClean);
        Assert.Equal(now, state.LastShutdownTime);
        Assert.Equal(SessionExitReason.ApplicationExitConnected, state.LastExitReason);
    }
}
