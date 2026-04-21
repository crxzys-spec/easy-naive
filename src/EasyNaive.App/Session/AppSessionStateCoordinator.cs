using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Session;

internal static class AppSessionStateCoordinator
{
    public static AppSessionLaunchInfo BeginLaunch(AppSessionState state, DateTimeOffset now)
    {
        var launchInfo = new AppSessionLaunchInfo(
            HadUncleanShutdown: !state.LastShutdownWasClean,
            PreviousExitReason: state.LastExitReason,
            PreviousRecoveryError: state.LastRecoveryError);

        state.LastLaunchTime = now;
        state.LastShutdownWasClean = false;
        state.LastExitReason = SessionExitReason.Running;
        return launchInfo;
    }

    public static bool ShouldPreserveRestoreConnection(CoreStatus coreStatus, bool isProcessRunning)
    {
        return coreStatus == CoreStatus.Running ||
               coreStatus == CoreStatus.Starting ||
               isProcessRunning;
    }

    public static SessionExitReason DetermineApplicationExitReason(CoreStatus coreStatus, bool isProcessRunning)
    {
        return ShouldPreserveRestoreConnection(coreStatus, isProcessRunning)
            ? SessionExitReason.ApplicationExitConnected
            : SessionExitReason.ApplicationExitDisconnected;
    }

    public static void MarkStartupRecoveryFailed(AppSessionState state, string message, DateTimeOffset now)
    {
        state.RestoreConnectionOnLaunch = false;
        state.LastExitReason = SessionExitReason.RecoveryFailed;
        state.LastRecoveryAttemptTime = now;
        state.LastRecoveryError = message;
    }

    public static void MarkStartupRecoverySucceeded(AppSessionState state, DateTimeOffset now)
    {
        state.LastRecoveryAttemptTime = now;
        state.LastRecoveryError = string.Empty;
    }

    public static void MarkGracefulStop(AppSessionState state, bool preserveRestoreConnection)
    {
        state.RestoreConnectionOnLaunch = preserveRestoreConnection;
        state.LastExitReason = preserveRestoreConnection
            ? SessionExitReason.ApplicationExitConnected
            : SessionExitReason.ManualDisconnect;
    }

    public static void MarkUnexpectedTermination(AppSessionState state)
    {
        state.RestoreConnectionOnLaunch = true;
        state.LastExitReason = SessionExitReason.UnexpectedTermination;
    }

    public static void MarkDisposed(AppSessionState state, SessionExitReason exitReason, DateTimeOffset now)
    {
        state.LastShutdownWasClean = true;
        state.LastShutdownTime = now;
        state.LastExitReason = exitReason;
    }
}
