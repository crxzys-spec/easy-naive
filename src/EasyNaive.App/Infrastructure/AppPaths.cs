using EasyNaive.SingBox.Config;

namespace EasyNaive.App.Infrastructure;

internal sealed class AppPaths
{
    private AppPaths(string singBoxDirectory, string dataRoot)
    {
        SingBoxDirectory = singBoxDirectory;
        DataRoot = dataRoot;
    }

    public string SingBoxDirectory { get; }

    public string SingBoxExecutablePath => Path.Combine(SingBoxDirectory, "sing-box.exe");

    public string ElevationExecutablePath => ResolveElevationExecutablePath(AppContext.BaseDirectory);

    public string DataRoot { get; }

    public string DataDirectory => Path.Combine(DataRoot, "data");

    public string ConfigDirectory => Path.Combine(DataRoot, "config");

    public string StateDirectory => Path.Combine(DataRoot, "state");

    public string CacheDirectory => Path.Combine(DataRoot, "cache");

    public string LogsDirectory => Path.Combine(DataRoot, "logs");

    public string RulesDirectory => Path.Combine(DataRoot, "rules");

    public string SettingsPath => Path.Combine(StateDirectory, "settings.json");

    public string AppStatePath => Path.Combine(StateDirectory, "app-state.json");

    public string ElevationSessionPath => Path.Combine(StateDirectory, "elevation-session.json");

    public string NodesPath => Path.Combine(DataDirectory, "nodes.json");

    public string SubscriptionsPath => Path.Combine(DataDirectory, "subscriptions.json");

    public string ActiveConfigPath => Path.Combine(ConfigDirectory, "active.json");

    public string SingBoxLogPath => Path.Combine(LogsDirectory, "sing-box.log");

    public string AppLogPath => Path.Combine(LogsDirectory, "app.log");

    public string CacheFilePath => Path.Combine(CacheDirectory, "cache.db");

    public string CnDomainRuleSetPath => Path.Combine(RulesDirectory, "cn-domain.srs");

    public string CnIpRuleSetPath => Path.Combine(RulesDirectory, "cn-ip.srs");

    public static AppPaths CreateDefault()
    {
        var singBoxDirectory = ResolveSingBoxDirectory(AppContext.BaseDirectory);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataRoot = Path.Combine(localAppData, "EasyNaive");

        return new AppPaths(singBoxDirectory, dataRoot);
    }

    internal static AppPaths CreateForTesting(string singBoxDirectory, string dataRoot)
    {
        return new AppPaths(singBoxDirectory, dataRoot);
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RulesDirectory);
    }

    public SingBoxBuildContext CreateBuildContext(int clashApiPort)
    {
        return new SingBoxBuildContext
        {
            CacheFilePath = CacheFilePath,
            CnDomainRuleSetPath = CnDomainRuleSetPath,
            CnIpRuleSetPath = CnIpRuleSetPath,
            ClashApiController = $"127.0.0.1:{clashApiPort}"
        };
    }

    private static string ResolveSingBoxDirectory(string baseDirectory)
    {
        var installedCandidate = Path.Combine(baseDirectory, "sing-box", "sing-box.exe");
        if (File.Exists(installedCandidate))
        {
            return Path.GetDirectoryName(installedCandidate)!;
        }

        var directory = new DirectoryInfo(baseDirectory);

        while (directory is not null)
        {
            var repoCandidate = Path.Combine(directory.FullName, "bin", "sing-box", "sing-box.exe");
            if (File.Exists(repoCandidate))
            {
                return Path.GetDirectoryName(repoCandidate)!;
            }

            directory = directory.Parent;
        }

        return Path.Combine(baseDirectory, "sing-box");
    }

    private static string ResolveElevationExecutablePath(string baseDirectory)
    {
        var sameDirectoryCandidate = Path.Combine(baseDirectory, "EasyNaive.Elevation.exe");
        if (File.Exists(sameDirectoryCandidate))
        {
            return sameDirectoryCandidate;
        }

        var currentDirectory = new DirectoryInfo(baseDirectory);
        var targetFrameworkDirectory = currentDirectory.Name;
        var configurationDirectory = currentDirectory.Parent?.Name;
        var appProjectDirectory = currentDirectory.Parent?.Parent?.Name;
        var binDirectory = currentDirectory.Parent?.Parent?.Parent;

        if (string.Equals(appProjectDirectory, "EasyNaive.App", StringComparison.OrdinalIgnoreCase) &&
            binDirectory is not null)
        {
            var siblingCandidate = Path.Combine(
                binDirectory.FullName,
                "EasyNaive.Elevation",
                configurationDirectory ?? "Debug",
                targetFrameworkDirectory,
                "EasyNaive.Elevation.exe");

            if (File.Exists(siblingCandidate))
            {
                return siblingCandidate;
            }
        }

        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            var debugCandidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "bin",
                "EasyNaive.Elevation",
                "Debug",
                targetFrameworkDirectory,
                "EasyNaive.Elevation.exe");

            if (File.Exists(debugCandidate))
            {
                return debugCandidate;
            }

            var releaseCandidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "bin",
                "EasyNaive.Elevation",
                "Release",
                targetFrameworkDirectory,
                "EasyNaive.Elevation.exe");

            if (File.Exists(releaseCandidate))
            {
                return releaseCandidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(baseDirectory, "EasyNaive.Elevation.exe");
    }
}
