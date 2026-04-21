using EasyNaive.Core.Enums;

namespace EasyNaive.Core.Models;

public sealed class RuntimeState
{
    public CoreStatus CoreStatus { get; set; } = CoreStatus.Stopped;

    public string StatusDetail { get; set; } = "Disconnected";

    public string CurrentProfileHash { get; set; } = string.Empty;

    public string LastError { get; set; } = string.Empty;

    public string CurrentRealNodeId { get; set; } = string.Empty;

    public int? CurrentLatency { get; set; }

    public long UploadRateBytesPerSecond { get; set; }

    public long DownloadRateBytesPerSecond { get; set; }

    public long UploadTotalBytes { get; set; }

    public long DownloadTotalBytes { get; set; }

    public int ActiveConnections { get; set; }

    public DateTimeOffset? LastStartTime { get; set; }

    public int? ProcessId { get; set; }

    public int? ElevationHelperProcessId { get; set; }

    public int? ElevationSingBoxProcessId { get; set; }

    public string ElevationStatusDetail { get; set; } = string.Empty;
}
