using EasyNaive.Core.Enums;

namespace EasyNaive.App.Session;

internal readonly record struct AppSessionLaunchInfo(
    bool HadUncleanShutdown,
    SessionExitReason PreviousExitReason,
    string PreviousRecoveryError);
