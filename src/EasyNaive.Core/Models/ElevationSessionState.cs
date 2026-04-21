using EasyNaive.Core.Enums;

namespace EasyNaive.Core.Models;

public sealed class ElevationSessionState
{
    public ElevationSessionStatus Status { get; set; } = ElevationSessionStatus.Unknown;

    public int HelperProcessId { get; set; }

    public int SingBoxProcessId { get; set; }

    public string ConfigPath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string LogPath { get; set; } = string.Empty;

    public string ElevationExecutablePath { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string Error { get; set; } = string.Empty;

    public int? ExitCode { get; set; }
}
