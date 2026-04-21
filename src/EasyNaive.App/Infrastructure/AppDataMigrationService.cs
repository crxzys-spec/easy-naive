namespace EasyNaive.App.Infrastructure;

internal sealed class AppDataMigrationService
{
    private readonly AppPaths _paths;

    public AppDataMigrationService(AppPaths paths)
    {
        _paths = paths;
    }

    public AppDataMigrationReport MigrateLegacyLayout()
    {
        var report = new AppDataMigrationReport();

        foreach (var legacyRoot in GetLegacyRoots())
        {
            foreach (var (sourcePath, targetPath) in EnumerateMappings(legacyRoot))
            {
                TryCopyLegacyFile(sourcePath, targetPath, report);
            }
        }

        return report;
    }

    private IEnumerable<string> GetLegacyRoots()
    {
        yield return _paths.DataRoot;

        var roamingDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyNaive");

        if (!string.Equals(roamingDataRoot, _paths.DataRoot, StringComparison.OrdinalIgnoreCase))
        {
            yield return roamingDataRoot;
        }
    }

    private IEnumerable<(string SourcePath, string TargetPath)> EnumerateMappings(string legacyRoot)
    {
        yield return (Path.Combine(legacyRoot, "settings.json"), _paths.SettingsPath);
        yield return (Path.Combine(legacyRoot, "app-state.json"), _paths.AppStatePath);
        yield return (Path.Combine(legacyRoot, "elevation-session.json"), _paths.ElevationSessionPath);
        yield return (Path.Combine(legacyRoot, "nodes.json"), _paths.NodesPath);
        yield return (Path.Combine(legacyRoot, "subscriptions.json"), _paths.SubscriptionsPath);
        yield return (Path.Combine(legacyRoot, "active.json"), _paths.ActiveConfigPath);
        yield return (Path.Combine(legacyRoot, "cache.db"), _paths.CacheFilePath);
        yield return (Path.Combine(legacyRoot, "app.log"), _paths.AppLogPath);
        yield return (Path.Combine(legacyRoot, "sing-box.log"), _paths.SingBoxLogPath);
        yield return (Path.Combine(legacyRoot, "cn-domain.srs"), _paths.CnDomainRuleSetPath);
        yield return (Path.Combine(legacyRoot, "cn-ip.srs"), _paths.CnIpRuleSetPath);

        yield return (Path.Combine(legacyRoot, "state", "settings.json"), _paths.SettingsPath);
        yield return (Path.Combine(legacyRoot, "state", "app-state.json"), _paths.AppStatePath);
        yield return (Path.Combine(legacyRoot, "state", "elevation-session.json"), _paths.ElevationSessionPath);
        yield return (Path.Combine(legacyRoot, "data", "nodes.json"), _paths.NodesPath);
        yield return (Path.Combine(legacyRoot, "data", "subscriptions.json"), _paths.SubscriptionsPath);
        yield return (Path.Combine(legacyRoot, "config", "active.json"), _paths.ActiveConfigPath);
        yield return (Path.Combine(legacyRoot, "cache", "cache.db"), _paths.CacheFilePath);
        yield return (Path.Combine(legacyRoot, "logs", "app.log"), _paths.AppLogPath);
        yield return (Path.Combine(legacyRoot, "logs", "sing-box.log"), _paths.SingBoxLogPath);
        yield return (Path.Combine(legacyRoot, "rules", "cn-domain.srs"), _paths.CnDomainRuleSetPath);
        yield return (Path.Combine(legacyRoot, "rules", "cn-ip.srs"), _paths.CnIpRuleSetPath);
    }

    private static void TryCopyLegacyFile(string sourcePath, string targetPath, AppDataMigrationReport report)
    {
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(sourcePath) || File.Exists(targetPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: false);
            report.AddCopied(sourcePath, targetPath);
        }
        catch (Exception ex)
        {
            report.AddFailed(sourcePath, targetPath, ex);
        }
    }
}
