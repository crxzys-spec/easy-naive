using System.Text.Json;
using System.Text.Json.Serialization;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace EasyNaive.SingBox.Process;

public static class ElevationSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public static ElevationSessionState? TryRead(string sessionPath)
    {
        if (!File.Exists(sessionPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(sessionPath);
            return JsonSerializer.Deserialize<ElevationSessionState>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string sessionPath, ElevationSessionState session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(session, SerializerOptions));
    }

    public static void MarkStopped(string sessionPath, int? exitCode = null, string error = "")
    {
        var session = TryRead(sessionPath);
        if (session is null)
        {
            return;
        }

        session.Status = ElevationSessionStatus.Stopped;
        session.UpdatedAt = DateTimeOffset.Now;
        session.ExitCode = exitCode ?? session.ExitCode;
        session.Error = error;
        Write(sessionPath, session);
    }

    public static ElevationSessionSummary GetSummary(string sessionPath)
    {
        if (!File.Exists(sessionPath))
        {
            return new ElevationSessionSummary
            {
                Exists = false,
                IsReadable = false,
                Detail = "No elevation session file."
            };
        }

        var session = TryRead(sessionPath);
        if (session is null)
        {
            return new ElevationSessionSummary
            {
                Exists = true,
                IsReadable = false,
                Detail = $"Elevation session file exists but could not be parsed: {sessionPath}"
            };
        }

        var isSingBoxAlive = IsProcessAlive(session.SingBoxProcessId);
        return new ElevationSessionSummary
        {
            Exists = true,
            IsReadable = true,
            Status = session.Status,
            HelperProcessId = session.HelperProcessId,
            SingBoxProcessId = session.SingBoxProcessId,
            IsSingBoxProcessAlive = isSingBoxAlive,
            Error = session.Error,
            ExitCode = session.ExitCode,
            UpdatedAt = session.UpdatedAt,
            Detail = BuildDetail(session, isSingBoxAlive)
        };
    }

    private static string BuildDetail(ElevationSessionState session, bool isSingBoxAlive)
    {
        var parts = new List<string>
        {
            $"Status={session.Status}"
        };

        if (session.HelperProcessId > 0)
        {
            parts.Add($"HelperPID={session.HelperProcessId}");
        }

        if (session.SingBoxProcessId > 0)
        {
            parts.Add($"SingBoxPID={session.SingBoxProcessId}");
            parts.Add($"SingBoxAlive={isSingBoxAlive}");
        }

        if (session.ExitCode is int exitCode)
        {
            parts.Add($"ExitCode={exitCode}");
        }

        if (!string.IsNullOrWhiteSpace(session.Error))
        {
            parts.Add($"Error={session.Error}");
        }

        if (session.UpdatedAt != default)
        {
            parts.Add($"Updated={session.UpdatedAt:u}");
        }

        return string.Join(", ", parts);
    }

    private static bool IsProcessAlive(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = DiagnosticsProcess.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
