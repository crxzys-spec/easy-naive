using EasyNaive.App.Infrastructure;
using EasyNaive.App.Subscriptions;
using EasyNaive.App.Tray;
using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.DataProtection;
using EasyNaive.Platform.Windows.Proxy;
using EasyNaive.Platform.Windows.Shell;
using EasyNaive.Platform.Windows.Startup;
using EasyNaive.SingBox.ClashApi;
using EasyNaive.SingBox.Config;
using EasyNaive.SingBox.Process;

namespace EasyNaive.App;

internal static class AppBootstrapper
{
    public static TrayApplicationContext Create()
    {
        var paths = AppPaths.CreateDefault();
        paths.EnsureDirectories();
        var migrationReport = new AppDataMigrationService(paths).MigrateLegacyLayout();

        var dataProtection = new WindowsDataProtection();
        var settingsStore = new JsonFileStore<AppSettings>(
            paths.SettingsPath,
            new AppSettingsSecretTransform(dataProtection));
        var appStateStore = new JsonFileStore<AppSessionState>(paths.AppStatePath);
        var nodesStore = new JsonFileStore<List<NodeProfile>>(
            paths.NodesPath,
            new NodeProfileSecretTransform(dataProtection));
        var subscriptionsStore = new JsonFileStore<List<SubscriptionProfile>>(
            paths.SubscriptionsPath,
            new SubscriptionProfileSecretTransform(dataProtection));

        var settings = settingsStore.LoadOrCreate(AppSettings.CreateDefault);
        var appState = appStateStore.LoadOrCreate(() => new AppSessionState());
        var nodes = nodesStore.LoadOrCreate(() => new List<NodeProfile>());
        var subscriptions = subscriptionsStore.LoadOrCreate(() => new List<SubscriptionProfile>());

        var appLogger = new FileAppLogger(paths.AppLogPath);
        migrationReport.WriteTo(appLogger);

        var controller = new CoreController(
            settings,
            appState,
            nodes,
            subscriptions,
            paths,
            settingsStore,
            appStateStore,
            nodesStore,
            subscriptionsStore,
            appLogger,
            new WindowsStartupManager(),
            new WindowsSystemProxyManager(),
            new WindowsShellManager(),
            new RuleSetUpdateService(paths),
            new ClashApiClient(),
            new SubscriptionImportService(),
            new SingBoxConfigBuilder(),
            new SingBoxProcessOrchestrator(
                new SingBoxProcessManager(),
                new ElevatedSingBoxProcessManager()));

        return new TrayApplicationContext(controller);
    }
}
