namespace EasyNaive.Core.Enums;

public enum SessionExitReason
{
    Unknown,
    Running,
    ManualDisconnect,
    ApplicationExitDisconnected,
    ApplicationExitConnected,
    UnexpectedTermination,
    RecoveryFailed
}
