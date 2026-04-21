namespace EasyNaive.SingBox.Service;

public static class SingBoxServiceProtocol
{
    public const string PipeName = "EasyNaive.Service";
    public const string ServiceName = "EasyNaiveService";

    public const string StartCommand = "start";
    public const string StopCommand = "stop";
    public const string StatusCommand = "status";
}

public sealed class SingBoxServiceRequest
{
    public string Command { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string ConfigPath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string LogPath { get; set; } = string.Empty;

    public string SessionPath { get; set; } = string.Empty;
}

public sealed class SingBoxServiceResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int? ProcessId { get; set; }

    public int? ExitCode { get; set; }
}
