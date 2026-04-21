namespace EasyNaive.SingBox.Process;

public sealed class SingBoxStartOptions
{
    public required string ExecutablePath { get; init; }

    public required string ConfigPath { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string LogPath { get; init; }

    public bool RequiresElevation { get; init; }

    public string? ElevationExecutablePath { get; init; }

    public string? ElevationSessionPath { get; init; }
}
