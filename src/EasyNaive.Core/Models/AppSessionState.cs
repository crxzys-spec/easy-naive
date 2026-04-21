using EasyNaive.Core.Enums;

namespace EasyNaive.Core.Models;

public sealed class AppSessionState
{
    public bool RestoreConnectionOnLaunch { get; set; }

    public bool LastShutdownWasClean { get; set; } = true;

    public SessionExitReason LastExitReason { get; set; } = SessionExitReason.Unknown;

    public DateTimeOffset? LastLaunchTime { get; set; }

    public DateTimeOffset? LastShutdownTime { get; set; }

    public DateTimeOffset? LastRecoveryAttemptTime { get; set; }

    public string LastRecoveryError { get; set; } = string.Empty;

    public SystemProxySnapshot? SystemProxySnapshot { get; set; }
}
