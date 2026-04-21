using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;

namespace EasyNaive.Elevation;

internal static class Program
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private static readonly object LogSyncRoot = new();

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                return Fail("Missing command.");
            }

            if (!IsAdministrator())
            {
                return Fail("Administrator privileges are required.");
            }

            return args[0].ToLowerInvariant() switch
            {
                "start" => RunStart(args),
                "stop" => RunStop(args),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.ToString());
        }
    }

    private static int RunStart(IReadOnlyList<string> args)
    {
        if (args.Count < 6)
        {
            return Fail("Usage: start <sessionPath> <singBoxPath> <configPath> <workingDirectory> <logPath>");
        }

        var sessionPath = args[1];
        var singBoxPath = args[2];
        var configPath = args[3];
        var workingDirectory = args[4];
        var logPath = args[5];

        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Process? singBoxProcess = null;
        StreamWriter? logWriter = null;

        try
        {
            logWriter = CreateLogWriter(logPath);
            WriteLogLine(logWriter, $"[{DateTimeOffset.Now:u}] [helper] Starting elevated sing-box using config: {configPath}");

            singBoxProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = singBoxPath,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = false
            };

            singBoxProcess.StartInfo.ArgumentList.Add("run");
            singBoxProcess.StartInfo.ArgumentList.Add("-c");
            singBoxProcess.StartInfo.ArgumentList.Add(configPath);

            singBoxProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    WriteLogLine(logWriter, e.Data);
                }
            };
            singBoxProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    WriteLogLine(logWriter, e.Data);
                }
            };

            if (!singBoxProcess.Start())
            {
                throw new InvalidOperationException("Failed to start sing-box in the elevated helper.");
            }

            singBoxProcess.BeginOutputReadLine();
            singBoxProcess.BeginErrorReadLine();

            WriteSession(sessionPath, new ElevationSessionState
            {
                Status = ElevationSessionStatus.Running,
                HelperProcessId = Environment.ProcessId,
                SingBoxProcessId = singBoxProcess.Id,
                ConfigPath = configPath,
                WorkingDirectory = workingDirectory,
                LogPath = logPath,
                ElevationExecutablePath = Environment.ProcessPath ?? string.Empty,
                UpdatedAt = DateTimeOffset.Now
            });

            singBoxProcess.WaitForExit();

            WriteLogLine(logWriter, $"[{DateTimeOffset.Now:u}] [helper] Elevated sing-box exited with code {singBoxProcess.ExitCode}.");
            WriteSession(sessionPath, new ElevationSessionState
            {
                Status = ElevationSessionStatus.Stopped,
                HelperProcessId = Environment.ProcessId,
                SingBoxProcessId = singBoxProcess.Id,
                ConfigPath = configPath,
                WorkingDirectory = workingDirectory,
                LogPath = logPath,
                ElevationExecutablePath = Environment.ProcessPath ?? string.Empty,
                UpdatedAt = DateTimeOffset.Now,
                ExitCode = singBoxProcess.ExitCode
            });

            return 0;
        }
        catch (Exception ex)
        {
            WriteSession(sessionPath, new ElevationSessionState
            {
                Status = ElevationSessionStatus.Failed,
                HelperProcessId = Environment.ProcessId,
                ConfigPath = configPath,
                WorkingDirectory = workingDirectory,
                LogPath = logPath,
                ElevationExecutablePath = Environment.ProcessPath ?? string.Empty,
                UpdatedAt = DateTimeOffset.Now,
                Error = ex.Message
            });

            if (logWriter is not null)
            {
                WriteLogLine(logWriter, $"[{DateTimeOffset.Now:u}] [helper] Startup failed: {ex}");
            }

            return 1;
        }
        finally
        {
            singBoxProcess?.Dispose();
            logWriter?.Dispose();
        }
    }

    private static int RunStop(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            return Fail("Usage: stop <sessionPath>");
        }

        var sessionPath = args[1];
        var session = TryReadSession(sessionPath);
        if (session is null)
        {
            return 0;
        }

        var logWriter = string.IsNullOrWhiteSpace(session.LogPath) ? null : CreateLogWriter(session.LogPath);

        try
        {
            WriteLogLine(logWriter, $"[{DateTimeOffset.Now:u}] [helper] Stop requested for elevated TUN session.");

            if (session.SingBoxProcessId > 0)
            {
                TryKillProcess(session.SingBoxProcessId);
            }

            if (session.HelperProcessId > 0 && session.HelperProcessId != Environment.ProcessId)
            {
                WaitForProcessExit(session.HelperProcessId, TimeSpan.FromSeconds(5));
            }

            session.Status = ElevationSessionStatus.Stopped;
            session.UpdatedAt = DateTimeOffset.Now;
            session.Error = string.Empty;
            WriteSession(sessionPath, session);

            WriteLogLine(logWriter, $"[{DateTimeOffset.Now:u}] [helper] Stop request completed.");
            return 0;
        }
        catch (Exception ex)
        {
            session.Status = ElevationSessionStatus.Failed;
            session.UpdatedAt = DateTimeOffset.Now;
            session.Error = ex.Message;
            WriteSession(sessionPath, session);
            WriteLogLine(logWriter, $"[{DateTimeOffset.Now:u}] [helper] Stop failed: {ex}");
            return 1;
        }
        finally
        {
            logWriter?.Dispose();
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static StreamWriter CreateLogWriter(string logPath)
    {
        return new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    private static void WriteLogLine(StreamWriter? writer, string message)
    {
        if (writer is null)
        {
            return;
        }

        lock (LogSyncRoot)
        {
            writer.WriteLine(message);
        }
    }

    private static void WriteSession(string sessionPath, ElevationSessionState session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(session, SerializerOptions));
    }

    private static ElevationSessionState? TryReadSession(string sessionPath)
    {
        if (!File.Exists(sessionPath))
        {
            return null;
        }

        var json = File.ReadAllText(sessionPath);
        return JsonSerializer.Deserialize<ElevationSessionState>(json, SerializerOptions);
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
            // Process already exited.
        }
    }

    private static void WaitForProcessExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.WaitForExit((int)timeout.TotalMilliseconds);
            }
        }
        catch (ArgumentException)
        {
            // Process already exited.
        }
    }

    private static int Fail(string message)
    {
        return 1;
    }
}
