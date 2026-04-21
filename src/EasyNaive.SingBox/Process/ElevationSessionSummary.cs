using EasyNaive.Core.Enums;

namespace EasyNaive.SingBox.Process;

public sealed class ElevationSessionSummary
{
    public bool Exists { get; init; }

    public bool IsReadable { get; init; }

    public ElevationSessionStatus Status { get; init; } = ElevationSessionStatus.Unknown;

    public int HelperProcessId { get; init; }

    public int SingBoxProcessId { get; init; }

    public bool IsSingBoxProcessAlive { get; init; }

    public string Error { get; init; } = string.Empty;

    public int? ExitCode { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public string Detail { get; init; } = string.Empty;
}
