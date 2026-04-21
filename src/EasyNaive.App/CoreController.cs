using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Net.Sockets;
using System.Net;
using EasyNaive.App.Diagnostics;
using EasyNaive.App.Infrastructure;
using EasyNaive.App.Presentation;
using EasyNaive.App.Session;
using EasyNaive.App.Subscriptions;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;
using EasyNaive.Platform.Windows.Proxy;
using EasyNaive.Platform.Windows.Shell;
using EasyNaive.Platform.Windows.Startup;
using EasyNaive.SingBox.ClashApi;
using EasyNaive.SingBox.Config;
using EasyNaive.SingBox.Process;
using EasyNaive.SingBox.Tags;

namespace EasyNaive.App;

internal sealed class CoreController : IDisposable
{
    private const string DefaultDelayTestUrl = "https://www.gstatic.com/generate_204";
    private const int DefaultDelayTimeoutMs = 5000;

    private readonly AppPaths _paths;
    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly JsonFileStore<AppSessionState> _appStateStore;
    private readonly JsonFileStore<List<NodeProfile>> _nodesStore;
    private readonly JsonFileStore<List<SubscriptionProfile>> _subscriptionsStore;
    private readonly FileAppLogger _appLogger;
    private readonly WindowsStartupManager _startupManager;
    private readonly WindowsSystemProxyManager _systemProxyManager;
    private readonly WindowsShellManager _shellManager;
    private readonly RuleSetUpdateService _ruleSetUpdateService;
    private readonly ClashApiClient _clashApiClient;
    private readonly SubscriptionImportService _subscriptionImportService;
    private readonly SingBoxConfigBuilder _configBuilder;
    private readonly SingBoxProcessOrchestrator _processManager;
    private readonly List<NodeProfile> _nodes;
    private readonly List<SubscriptionProfile> _subscriptions;
    private readonly AppSessionState _appSessionState;
    private readonly Dictionary<string, int> _nodeLatencies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _nodeLatencyErrors = new(StringComparer.Ordinal);
    private volatile bool _suppressProcessExitError;
    private CancellationTokenSource? _trafficMonitorCancellationTokenSource;
    private Task? _trafficMonitorTask;
    private bool _preserveRestoreConnectionOnNextDisconnect;
    private readonly bool _hadUncleanShutdown;
    private readonly SessionExitReason _previousExitReason;
    private readonly string _previousRecoveryError;
    private SessionExitReason _pendingApplicationExitReason = SessionExitReason.ApplicationExitDisconnected;
    private bool _shutdownCompleted;

    public CoreController(
        AppSettings settings,
        AppSessionState appSessionState,
        List<NodeProfile> nodes,
        List<SubscriptionProfile> subscriptions,
        AppPaths paths,
        JsonFileStore<AppSettings> settingsStore,
        JsonFileStore<AppSessionState> appStateStore,
        JsonFileStore<List<NodeProfile>> nodesStore,
        JsonFileStore<List<SubscriptionProfile>> subscriptionsStore,
        FileAppLogger appLogger,
        WindowsStartupManager startupManager,
        WindowsSystemProxyManager systemProxyManager,
        WindowsShellManager shellManager,
        RuleSetUpdateService ruleSetUpdateService,
        ClashApiClient clashApiClient,
        SubscriptionImportService subscriptionImportService,
        SingBoxConfigBuilder configBuilder,
        SingBoxProcessOrchestrator processManager)
    {
        Settings = settings;
        RuntimeState = new RuntimeState();
        _appSessionState = appSessionState;
        _paths = paths;
        _settingsStore = settingsStore;
        _appStateStore = appStateStore;
        _nodesStore = nodesStore;
        _subscriptionsStore = subscriptionsStore;
        _appLogger = appLogger;
        _startupManager = startupManager;
        _systemProxyManager = systemProxyManager;
        _shellManager = shellManager;
        _ruleSetUpdateService = ruleSetUpdateService;
        _clashApiClient = clashApiClient;
        _subscriptionImportService = subscriptionImportService;
        _configBuilder = configBuilder;
        _processManager = processManager;
        _processManager.Exited += ProcessManagerOnExited;
        _nodes = nodes;
        _subscriptions = subscriptions;
        var launchInfo = AppSessionStateCoordinator.BeginLaunch(_appSessionState, DateTimeOffset.Now);
        _previousExitReason = launchInfo.PreviousExitReason;
        _previousRecoveryError = launchInfo.PreviousRecoveryError;
        _hadUncleanShutdown = launchInfo.HadUncleanShutdown;

        NormalizeSortOrders();
        EnsureSelectedNode();
        Persist();
        try
        {
            SyncAutoStartRegistration();
        }
        catch
        {
            // Keep the application available even if startup registration is unavailable.
        }
        RecoverSystemProxyOnLaunch();
        _appLogger.Info("EasyNaive controller initialized.");
    }

    public AppSettings Settings { get; }

    public RuntimeState RuntimeState { get; }

    public event EventHandler? RuntimeStateChanged;

    public IReadOnlyList<NodeProfile> Nodes => _nodes;

    public IReadOnlyList<SubscriptionProfile> Subscriptions => _subscriptions;

    public bool ShouldRestoreConnectionOnLaunch => _appSessionState.RestoreConnectionOnLaunch;

    public bool HadUncleanShutdown => _hadUncleanShutdown;

    public SessionExitReason PreviousExitReason => _previousExitReason;

    public string PreviousRecoveryError => _previousRecoveryError;

    public NodeProfile? SelectedNode => _nodes.FirstOrDefault(node => node.Id == Settings.SelectedNodeId);

    public string GenerateConfigPreview() => _configBuilder.BuildJson(Settings, _nodes, _paths.CreateBuildContext(Settings.ClashApiPort));

    public async Task<string> UpdateRuleSetsAsync(CancellationToken cancellationToken = default)
    {
        var summary = await _ruleSetUpdateService.UpdateAsync(cancellationToken);

        if (_processManager.IsRunning)
        {
            await ConnectAsync(cancellationToken);
        }

        _appLogger.Info(summary.AnyUpdated ? "Rule-sets updated." : "Rule-sets checked with no changes.");
        return summary.ToDisplayText();
    }

    public AppSettings CreateSettingsDraft() => CloneSettings(Settings);

    public string GenerateStatusSummary()
    {
        var builder = new StringBuilder();
        builder.Append("Capture: ").Append(Settings.CaptureMode);
        builder.Append(" | Route: ").Append(Settings.RouteMode);
        builder.Append(" | Node Mode: ").Append(Settings.NodeMode);
        builder.Append(" | Nodes: ").Append(_nodes.Count);
        builder.Append(" | Subs: ").Append(_subscriptions.Count);
        builder.Append(" | Node: ").Append(GetCurrentNodeDisplayName());
        builder.Append(" | Core: ").Append(RuntimeState.CoreStatus);
        builder.Append(" | Stage: ").Append(RuntimeState.StatusDetail);
        builder.Append(" | PID: ").Append(RuntimeState.ProcessId?.ToString() ?? "-");
        builder.Append(" | Latency: ").Append(RuntimeState.CurrentLatency is int latency ? $"{latency} ms" : "-");
        builder.Append(" | Down: ").Append(TrafficFormatter.FormatRate(RuntimeState.DownloadRateBytesPerSecond));
        builder.Append(" | Up: ").Append(TrafficFormatter.FormatRate(RuntimeState.UploadRateBytesPerSecond));
        builder.Append(" | Conn: ").Append(RuntimeState.ActiveConnections);

        if (!string.IsNullOrWhiteSpace(RuntimeState.LastError))
        {
            builder.Append(" | Error: ").Append(RuntimeState.LastError);
        }

        return builder.ToString();
    }

    public NodeProfile CreateNodeDraft(string? nodeId = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return new NodeProfile
            {
                SortOrder = _nodes.Count
            };
        }

        var existing = _nodes.FirstOrDefault(node => node.Id == nodeId);
        if (existing is null)
        {
            throw new InvalidOperationException("Node was not found.");
        }

        return CloneNode(existing);
    }

    public SubscriptionProfile CreateSubscriptionDraft(string? subscriptionId = null)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return new SubscriptionProfile
            {
                Name = $"Subscription {_subscriptions.Count + 1}"
            };
        }

        var existing = _subscriptions.FirstOrDefault(subscription => subscription.Id == subscriptionId);
        if (existing is null)
        {
            throw new InvalidOperationException("Subscription was not found.");
        }

        return CloneSubscription(existing);
    }

    public string GetNodeLatencyDisplay(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return "-";
        }

        if (_nodeLatencies.TryGetValue(nodeId, out var delay))
        {
            return $"{delay} ms";
        }

        if (_nodeLatencyErrors.TryGetValue(nodeId, out var error))
        {
            return error;
        }

        return "-";
    }

    public async Task<int> ImportNodesFromTextAsync(string sourceName, string content, CancellationToken cancellationToken = default)
    {
        var normalizedSourceName = sourceName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSourceName))
        {
            throw new InvalidOperationException("Source name is required.");
        }

        var importSeed = new SubscriptionProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedSourceName,
            Url = "manual://import",
            Enabled = true
        };

        var importedNodes = _subscriptionImportService.ParseNodes(importSeed, content)
            .Select(node =>
            {
                var importedNode = CloneNode(node);
                importedNode.Id = Guid.NewGuid().ToString("N");
                importedNode.SubscriptionId = string.Empty;
                importedNode.Group = string.IsNullOrWhiteSpace(importedNode.Group) ? normalizedSourceName : importedNode.Group;
                return importedNode;
            })
            .ToArray();

        if (importedNodes.Length == 0)
        {
            throw new InvalidOperationException("No valid naive nodes were found in the import content.");
        }

        var nextSortOrder = _nodes.Count == 0 ? 0 : _nodes.Max(node => node.SortOrder) + 1;
        foreach (var importedNode in importedNodes)
        {
            importedNode.SortOrder = nextSortOrder++;
            _nodes.Add(importedNode);
        }

        NormalizeSortOrders();
        EnsureSelectedNode();
        RefreshCurrentLatency();

        await PersistAndRestartIfRunningAsync(cancellationToken);
        return importedNodes.Length;
    }

    public async Task AddSubscriptionAsync(SubscriptionProfile subscription, CancellationToken cancellationToken = default)
    {
        var created = CloneSubscription(subscription);
        created.Id = string.IsNullOrWhiteSpace(created.Id) ? Guid.NewGuid().ToString("N") : created.Id;

        ValidateSubscription(created, null);

        if (_subscriptions.Any(existing => existing.Id == created.Id))
        {
            throw new InvalidOperationException("A subscription with the same identifier already exists.");
        }

        _subscriptions.Add(created);

        try
        {
            await RefreshSubscriptionInternalAsync(created, cancellationToken);
        }
        catch
        {
            Persist();
            throw;
        }
    }

    public async Task UpdateSubscriptionAsync(SubscriptionProfile subscription, CancellationToken cancellationToken = default)
    {
        var existing = _subscriptions.FirstOrDefault(current => current.Id == subscription.Id);
        if (existing is null)
        {
            throw new InvalidOperationException("Subscription was not found.");
        }

        var updated = CloneSubscription(subscription);
        ValidateSubscription(updated, existing.Id);

        existing.Name = updated.Name;
        existing.Url = updated.Url;
        existing.Enabled = updated.Enabled;

        if (!existing.Enabled)
        {
            existing.LastError = string.Empty;
            existing.ImportedNodeCount = 0;
            ReplaceNodesForSubscription(existing.Id, Array.Empty<NodeProfile>());
            await PersistAndRestartIfRunningAsync(cancellationToken);
            return;
        }

        await RefreshSubscriptionInternalAsync(existing, cancellationToken);
    }

    public async Task RemoveSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var removed = _subscriptions.RemoveAll(subscription => subscription.Id == subscriptionId);
        if (removed == 0)
        {
            return;
        }

        ReplaceNodesForSubscription(subscriptionId, Array.Empty<NodeProfile>());
        await PersistAndRestartIfRunningAsync(cancellationToken);
    }

    public async Task RefreshSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = _subscriptions.FirstOrDefault(current => current.Id == subscriptionId);
        if (subscription is null)
        {
            throw new InvalidOperationException("Subscription was not found.");
        }

        await RefreshSubscriptionInternalAsync(subscription, cancellationToken);
    }

    public async Task RefreshAllSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var enabledSubscriptions = _subscriptions
            .Where(subscription => subscription.Enabled)
            .ToArray();

        if (enabledSubscriptions.Length == 0)
        {
            throw new InvalidOperationException("No enabled subscriptions are available.");
        }

        var results = new Dictionary<string, IReadOnlyList<NodeProfile>>(StringComparer.Ordinal);
        var failedSubscriptions = new List<string>();

        foreach (var subscription in enabledSubscriptions)
        {
            try
            {
                results[subscription.Id] = await DownloadSubscriptionNodesAsync(subscription, cancellationToken);
            }
            catch
            {
                failedSubscriptions.Add(subscription.Name);
            }
        }

        foreach (var pair in results)
        {
            ReplaceNodesForSubscription(pair.Key, pair.Value);
        }

        Persist();

        if (_processManager.IsRunning && results.Count > 0)
        {
            await ConnectAsync(cancellationToken);
        }

        if (failedSubscriptions.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to refresh {failedSubscriptions.Count} subscription(s): {string.Join(", ", failedSubscriptions.Take(3))}{(failedSubscriptions.Count > 3 ? "..." : string.Empty)}");
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await StopTrafficMonitorAsync();
        UpdateRuntimeState(state =>
        {
            state.CoreStatus = CoreStatus.Starting;
            state.StatusDetail = "Saving settings";
            state.LastError = string.Empty;
        });
        EnsureSelectedNode();
        _suppressProcessExitError = true;
        _appLogger.Info($"Connect requested. Capture={Settings.CaptureMode}, Route={Settings.RouteMode}, NodeMode={Settings.NodeMode}, SelectedNode={SelectedNode?.Name ?? "-"}.");
        var connectStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();

        void LogConnectStage(string stageName)
        {
            _appLogger.Info($"Connect stage completed: {stageName} in {stageStopwatch.ElapsedMilliseconds} ms, total {connectStopwatch.ElapsedMilliseconds} ms.");
            stageStopwatch.Restart();
        }

        try
        {
            Persist();
            UpdateRuntimeState(state => state.StatusDetail = "Preparing runtime directories");
            _paths.EnsureDirectories();
            LogConnectStage("prepare");

            if (Settings.RouteMode == RouteMode.Rule)
            {
                UpdateRuntimeState(state => state.StatusDetail = "Checking rule-sets");
                var ruleSetSummary = await _ruleSetUpdateService.EnsureAsync(cancellationToken);
                if (ruleSetSummary.HasWarnings)
                {
                    _appLogger.Info("Rule-set refresh completed with warnings: " + ruleSetSummary.ToDisplayText().Replace(Environment.NewLine, " | "));
                }
                LogConnectStage("rule-set check");
            }

            UpdateRuntimeState(state => state.StatusDetail = "Generating sing-box config");
            var configJson = GenerateConfigPreview();
            File.WriteAllText(_paths.ActiveConfigPath, configJson);

            UpdateRuntimeState(state => state.CurrentProfileHash = ComputeSha256(configJson));
            LogConnectStage("generate config");

            UpdateRuntimeState(state => state.StatusDetail = "Validating sing-box config");
            await _processManager.CheckConfigAsync(
                _paths.SingBoxExecutablePath,
                _paths.ActiveConfigPath,
                _paths.SingBoxDirectory,
                cancellationToken);
            LogConnectStage("sing-box check");

            UpdateRuntimeState(state => state.StatusDetail = Settings.CaptureMode == CaptureMode.Tun ? "Starting elevated TUN helper" : "Starting sing-box");
            await _processManager.StartAsync(new SingBoxStartOptions
            {
                ExecutablePath = _paths.SingBoxExecutablePath,
                ConfigPath = _paths.ActiveConfigPath,
                WorkingDirectory = _paths.SingBoxDirectory,
                LogPath = _paths.SingBoxLogPath,
                RequiresElevation = Settings.CaptureMode == CaptureMode.Tun,
                ElevationExecutablePath = _paths.ElevationExecutablePath,
                ElevationSessionPath = _paths.ElevationSessionPath
            }, cancellationToken);
            LogConnectStage("start sing-box");

            UpdateRuntimeState(state =>
            {
                state.ProcessId = _processManager.ProcessId;
                state.StatusDetail = "Waiting for Clash API";
                ApplyElevationSessionSummary(state);
            });
            await ApplyClashApiStateAsync(cancellationToken);
            LogConnectStage("apply Clash API state");
            ApplySystemProxyState();
            await ValidateSystemProxyStateAsync(cancellationToken);
            LogConnectStage("system proxy validation");

            UpdateRuntimeState(state =>
            {
                state.CoreStatus = CoreStatus.Running;
                state.ProcessId = _processManager.ProcessId;
                state.LastStartTime = DateTimeOffset.Now;
                state.LastError = string.Empty;
                state.StatusDetail = "Connected";
                ApplyElevationSessionSummary(state);
            });
            await RefreshCurrentRealNodeStateAsync(cancellationToken);
            RefreshCurrentLatency();
            LogConnectStage("resolve active node");
            _appSessionState.RestoreConnectionOnLaunch = true;
            _appSessionState.LastRecoveryError = string.Empty;
            Persist();
            StartTrafficMonitor();
            _appLogger.Info($"Connected. PID={_processManager.ProcessId?.ToString() ?? "-"}. TotalConnectMs={connectStopwatch.ElapsedMilliseconds}.");
        }
        catch (Exception ex)
        {
            await StopTrafficMonitorAsync();
            try
            {
                if (_processManager.IsRunning)
                {
                    await _processManager.StopAsync(CancellationToken.None);
                }
            }
            catch
            {
                // Startup already failed. Best effort cleanup only.
            }

            TryRestoreManagedSystemProxy();
            UpdateRuntimeState(state =>
            {
                state.CoreStatus = CoreStatus.Error;
                state.ProcessId = null;
                state.CurrentRealNodeId = string.Empty;
                state.CurrentLatency = null;
                state.LastError = ex.Message;
                state.StatusDetail = "Connect failed";
                ApplyElevationSessionSummary(state);
            });
            _appLogger.Error("Connect failed.", ex);
            _appLogger.Info($"Connect failed after {connectStopwatch.ElapsedMilliseconds} ms.");
            throw;
        }
        finally
        {
            _suppressProcessExitError = false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await StopTrafficMonitorAsync();
        UpdateRuntimeState(state =>
        {
            state.CoreStatus = CoreStatus.Stopping;
            state.StatusDetail = Settings.CaptureMode == CaptureMode.Tun ? "Stopping TUN helper" : "Stopping sing-box";
            state.LastError = string.Empty;
            ApplyElevationSessionSummary(state);
        });
        _suppressProcessExitError = true;
        _appLogger.Info("Disconnect requested.");

        try
        {
            await _processManager.StopAsync(cancellationToken);
            TryRestoreManagedSystemProxy();
            _appSessionState.RestoreConnectionOnLaunch = _preserveRestoreConnectionOnNextDisconnect;
            _appSessionState.LastExitReason = _preserveRestoreConnectionOnNextDisconnect
                ? SessionExitReason.ApplicationExitConnected
                : SessionExitReason.ManualDisconnect;
            _preserveRestoreConnectionOnNextDisconnect = false;
            Persist();
            UpdateRuntimeState(state =>
            {
                state.CoreStatus = CoreStatus.Stopped;
                state.ProcessId = null;
                state.CurrentRealNodeId = string.Empty;
                state.CurrentLatency = null;
                state.LastError = string.Empty;
                state.StatusDetail = "Disconnected";
                ApplyElevationSessionSummary(state);
            });
            _appLogger.Info("Disconnected.");
        }
        catch (Exception ex)
        {
            _preserveRestoreConnectionOnNextDisconnect = false;
            if (!_processManager.IsRunning)
            {
                TryRestoreManagedSystemProxy();
            }
            UpdateRuntimeState(state =>
            {
                state.CoreStatus = CoreStatus.Error;
                state.LastError = ex.Message;
                state.StatusDetail = "Disconnect failed";
                ApplyElevationSessionSummary(state);
            });
            _appLogger.Error("Disconnect failed.", ex);
            throw;
        }
        finally
        {
            _suppressProcessExitError = false;
        }
    }

    public Task RestartCoreAsync(CancellationToken cancellationToken = default)
    {
        _appLogger.Info("Core restart requested.");
        return ConnectAsync(cancellationToken);
    }

    public void PrepareForApplicationExit()
    {
        _pendingApplicationExitReason = AppSessionStateCoordinator.DetermineApplicationExitReason(RuntimeState.CoreStatus, _processManager.IsRunning);
        _preserveRestoreConnectionOnNextDisconnect = AppSessionStateCoordinator.ShouldPreserveRestoreConnection(RuntimeState.CoreStatus, _processManager.IsRunning);
    }

    public void MarkStartupRecoveryFailed(string message)
    {
        AppSessionStateCoordinator.MarkStartupRecoveryFailed(_appSessionState, message, DateTimeOffset.Now);
        UpdateRuntimeState(state =>
        {
            state.CoreStatus = CoreStatus.Stopped;
            state.ProcessId = null;
            state.StatusDetail = "Recovery failed, waiting for manual connect";
            state.LastError = message;
            state.CurrentRealNodeId = string.Empty;
            state.CurrentLatency = null;
            ApplyElevationSessionSummary(state);
            ResetTrafficState(state);
        });
        Persist();
        _appLogger.Error("Startup recovery failed: " + message);
    }

    public void MarkStartupRecoverySucceeded()
    {
        AppSessionStateCoordinator.MarkStartupRecoverySucceeded(_appSessionState, DateTimeOffset.Now);
        Persist();
        _appLogger.Info("Startup recovery succeeded.");
    }

    public Task SetAutoStartAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (Settings.EnableAutoStart == enabled)
        {
            return Task.CompletedTask;
        }

        var previousValue = Settings.EnableAutoStart;
        Settings.EnableAutoStart = enabled;
        try
        {
            SyncAutoStartRegistration();
            Persist();
            _appLogger.Info($"Auto-start {(enabled ? "enabled" : "disabled")}.");
        }
        catch
        {
            Settings.EnableAutoStart = previousValue;
            throw;
        }

        return Task.CompletedTask;
    }

    public Task SetCaptureModeAsync(CaptureMode captureMode, CancellationToken cancellationToken = default)
    {
        if (Settings.CaptureMode == captureMode)
        {
            return Task.CompletedTask;
        }

        Settings.CaptureMode = captureMode;
        if (captureMode != CaptureMode.Proxy)
        {
            TryRestoreManagedSystemProxy();
        }
        return PersistAndRestartIfRunningAsync(cancellationToken);
    }

    public Task SetRouteModeAsync(RouteMode routeMode, CancellationToken cancellationToken = default)
    {
        if (Settings.RouteMode == routeMode)
        {
            return Task.CompletedTask;
        }

        Settings.RouteMode = routeMode;
        return PersistAndHotApplyIfRunningAsync(ApplyRouteModeTransitionAsync, cancellationToken);
    }

    public Task SetNodeModeAsync(NodeMode nodeMode, CancellationToken cancellationToken = default)
    {
        if (Settings.NodeMode == nodeMode)
        {
            return Task.CompletedTask;
        }

        Settings.NodeMode = nodeMode;
        RefreshCurrentLatency();
        return PersistAndHotApplyIfRunningAsync(ApplyNodeModeAsync, cancellationToken);
    }

    public Task SetSelectedNodeAsync(string? nodeId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(nodeId) && _nodes.All(node => node.Id != nodeId))
        {
            throw new InvalidOperationException("Node was not found.");
        }

        Settings.SelectedNodeId = nodeId;
        EnsureSelectedNode();
        RefreshCurrentLatency();
        return PersistAndHotApplyIfRunningAsync(ApplySelectedNodeAsync, cancellationToken);
    }

    public async Task UpdateGeneralSettingsAsync(AppSettings updatedSettings, CancellationToken cancellationToken = default)
    {
        var sanitized = CloneSettings(updatedSettings);
        ValidateGeneralSettings(sanitized);

        var previousSettings = CloneSettings(Settings);
        var autoStartChanged = previousSettings.EnableAutoStart != sanitized.EnableAutoStart;
        var requiresRestart = _processManager.IsRunning &&
            (previousSettings.ClashApiPort != sanitized.ClashApiPort ||
             !string.Equals(previousSettings.LogLevel, sanitized.LogLevel, StringComparison.OrdinalIgnoreCase) ||
             (Settings.CaptureMode == CaptureMode.Proxy && previousSettings.ProxyMixedPort != sanitized.ProxyMixedPort) ||
             (Settings.CaptureMode == CaptureMode.Tun && previousSettings.EnableTunStrictRoute != sanitized.EnableTunStrictRoute));

        if (!_processManager.IsRunning)
        {
            if (previousSettings.ProxyMixedPort != sanitized.ProxyMixedPort)
            {
                EnsureLoopbackPortAvailable(sanitized.ProxyMixedPort);
            }

            if (previousSettings.ClashApiPort != sanitized.ClashApiPort)
            {
                EnsureLoopbackPortAvailable(sanitized.ClashApiPort);
            }
        }

        ApplyGeneralSettings(sanitized);

        try
        {
            if (autoStartChanged)
            {
                SyncAutoStartRegistration();
            }

            Persist();

            if (requiresRestart)
            {
                await ConnectAsync(cancellationToken);
            }
        }
        catch
        {
            ApplyGeneralSettings(previousSettings);

            try
            {
                if (autoStartChanged)
                {
                    SyncAutoStartRegistration();
                }
            }
            catch
            {
                // Preserve the original exception from the failed update.
            }

            Persist();

            if (requiresRestart && !_processManager.IsRunning)
            {
                try
                {
                    await ConnectAsync(CancellationToken.None);
                }
                catch
                {
                    // Best effort restore only. Preserve the original settings update failure.
                }
            }

            throw;
        }

        _appLogger.Info("General settings updated.");
    }

    public Task AddNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
    {
        var created = CloneNode(node);
        created.Id = string.IsNullOrWhiteSpace(created.Id) ? Guid.NewGuid().ToString("N") : created.Id;
        created.SortOrder = _nodes.Count == 0 ? 0 : Math.Max(0, created.SortOrder);

        ValidateNode(created, null);

        if (_nodes.Any(existing => existing.Id == created.Id))
        {
            throw new InvalidOperationException("A node with the same identifier already exists.");
        }

        _nodes.Add(created);
        NormalizeSortOrders();
        EnsureSelectedNode();
        RefreshCurrentLatency();
        return PersistAndRestartIfRunningAsync(cancellationToken);
    }

    public Task UpdateNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
    {
        var existing = _nodes.FirstOrDefault(current => current.Id == node.Id);
        if (existing is null)
        {
            throw new InvalidOperationException("Node was not found.");
        }

        var updated = CloneNode(node);
        ValidateNode(updated, existing.Id);

        existing.Name = updated.Name;
        existing.Group = updated.Group;
        existing.Server = updated.Server;
        existing.ServerPort = updated.ServerPort;
        existing.Username = updated.Username;
        existing.Password = updated.Password;
        existing.TlsServerName = updated.TlsServerName;
        existing.UseQuic = updated.UseQuic;
        existing.UseUdpOverTcp = updated.UseUdpOverTcp;
        existing.Enabled = updated.Enabled;
        existing.SortOrder = updated.SortOrder;
        existing.Remark = updated.Remark;

        if (!existing.Enabled)
        {
            _nodeLatencies.Remove(existing.Id);
            _nodeLatencyErrors.Remove(existing.Id);
        }

        NormalizeSortOrders();
        EnsureSelectedNode();
        RefreshCurrentLatency();
        return PersistAndRestartIfRunningAsync(cancellationToken);
    }

    public Task RemoveNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var removedCount = _nodes.RemoveAll(node => node.Id == nodeId);
        if (removedCount == 0)
        {
            return Task.CompletedTask;
        }

        _nodeLatencies.Remove(nodeId);
        _nodeLatencyErrors.Remove(nodeId);
        NormalizeSortOrders();
        EnsureSelectedNode();
        RefreshCurrentLatency();
        return PersistAndRestartIfRunningAsync(cancellationToken);
    }

    public async Task<int> TestNodeLatencyAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var node = _nodes.FirstOrDefault(current => current.Id == nodeId);
        if (node is null)
        {
            throw new InvalidOperationException("Node was not found.");
        }

        return await TestNodeLatencyCoreAsync(node, cancellationToken);
    }

    public async Task TestAllNodeLatenciesAsync(CancellationToken cancellationToken = default)
    {
        EnsureLatencyTestReady();

        var failures = new List<string>();
        var enabledNodes = _nodes
            .Where(node => node.Enabled)
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var node in enabledNodes)
        {
            try
            {
                await TestNodeLatencyCoreAsync(node, cancellationToken);
            }
            catch
            {
                failures.Add(node.Name);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Latency test failed for {failures.Count} node(s): {string.Join(", ", failures.Take(3))}{(failures.Count > 3 ? "..." : string.Empty)}");
        }
    }

    public void OpenDataDirectory()
    {
        OpenDirectory(_paths.DataRoot);
    }

    public void OpenLogsDirectory()
    {
        _paths.EnsureDirectories();
        OpenDirectory(_paths.LogsDirectory);
    }

    public async Task<SelfCheckReport> RunSelfCheckAsync(CancellationToken cancellationToken = default)
    {
        _appLogger.Info("Self-check requested.");

        var report = new SelfCheckReport();
        _paths.EnsureDirectories();

        if (File.Exists(_paths.SingBoxExecutablePath))
        {
            report.AddPassed("sing-box executable", _paths.SingBoxExecutablePath);
        }
        else
        {
            report.AddFailed("sing-box executable", $"Missing: {_paths.SingBoxExecutablePath}");
        }

        var cronetPath = Path.Combine(_paths.SingBoxDirectory, "libcronet.dll");
        if (File.Exists(cronetPath))
        {
            report.AddPassed("libcronet", cronetPath);
        }
        else
        {
            report.AddFailed("libcronet", $"Missing: {cronetPath}");
        }

        if (File.Exists(_paths.ElevationExecutablePath))
        {
            report.AddPassed("Elevation helper", _paths.ElevationExecutablePath);
        }
        else
        {
            report.AddFailed("Elevation helper", $"Missing: {_paths.ElevationExecutablePath}");
        }

        AppendElevationSessionCheck(report);

        try
        {
            Directory.CreateDirectory(_paths.LogsDirectory);
            using var stream = new FileStream(_paths.AppLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            report.AddPassed("App log", _paths.AppLogPath);
        }
        catch (Exception ex)
        {
            report.AddFailed("App log", ex.Message);
        }

        try
        {
            var registered = _startupManager.IsEnabled(GetCurrentExecutablePath());
            if (registered == Settings.EnableAutoStart)
            {
                report.AddPassed("Startup registration", Settings.EnableAutoStart ? "Registry startup entry is enabled." : "Registry startup entry is disabled.");
            }
            else
            {
                report.AddFailed(
                    "Startup registration",
                    Settings.EnableAutoStart
                        ? "Settings expect auto-start to be enabled, but the registry entry is missing."
                        : "Settings expect auto-start to be disabled, but the registry entry still exists.");
            }
        }
        catch (Exception ex)
        {
            report.AddFailed("Startup registration", ex.Message);
        }

        var recoveryDetail = _appSessionState.RestoreConnectionOnLaunch
            ? "Automatic recovery is enabled for the next launch."
            : "Automatic recovery is disabled for the next launch.";
        if (!string.IsNullOrWhiteSpace(_previousRecoveryError))
        {
            recoveryDetail += $" Previous recovery error: {_previousRecoveryError}";
        }
        report.AddPassed("Session recovery", recoveryDetail);

        var selfCheckConfigPath = Path.Combine(_paths.ConfigDirectory, "self-check.json");
        try
        {
            File.WriteAllText(selfCheckConfigPath, GenerateConfigPreview());
            await _processManager.CheckConfigAsync(
                _paths.SingBoxExecutablePath,
                selfCheckConfigPath,
                _paths.SingBoxDirectory,
                cancellationToken);
            report.AddPassed("sing-box check", "Current generated config is valid.");
        }
        catch (Exception ex)
        {
            report.AddFailed("sing-box check", ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(selfCheckConfigPath))
                {
                    File.Delete(selfCheckConfigPath);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }
        }

        if (_processManager.IsRunning)
        {
            try
            {
                await _clashApiClient.WaitUntilAvailableAsync(GetClashApiController(), Settings.ClashApiSecret, cancellationToken);
                report.AddPassed("Clash API", $"Listening at {GetClashApiController()}.");
            }
            catch (Exception ex)
            {
                report.AddFailed("Clash API", ex.Message);
            }

            try
            {
                await VerifyLoopbackPortAsync("127.0.0.1", Settings.ClashApiPort, cancellationToken);
                report.AddPassed("Clash API port", $"127.0.0.1:{Settings.ClashApiPort} accepted a TCP connection.");
            }
            catch (Exception ex)
            {
                report.AddFailed("Clash API port", ex.Message);
            }

            if (Settings.CaptureMode == CaptureMode.Proxy)
            {
                try
                {
                    await VerifyLoopbackPortAsync("127.0.0.1", Settings.ProxyMixedPort, cancellationToken);
                    report.AddPassed("Mixed inbound", $"127.0.0.1:{Settings.ProxyMixedPort} accepted a TCP connection.");
                }
                catch (Exception ex)
                {
                    report.AddFailed("Mixed inbound", ex.Message);
                }
            }
            else
            {
                report.AddSkipped("Mixed inbound", "Current capture mode is TUN.");
            }
        }
        else
        {
            report.AddSkipped("Clash API", "Core is not running.");

            try
            {
                EnsureLoopbackPortAvailable(Settings.ClashApiPort);
                report.AddPassed("Clash API port", $"127.0.0.1:{Settings.ClashApiPort} is available.");
            }
            catch (Exception ex)
            {
                report.AddFailed("Clash API port", ex.Message);
            }

            if (Settings.CaptureMode == CaptureMode.Proxy)
            {
                try
                {
                    EnsureLoopbackPortAvailable(Settings.ProxyMixedPort);
                    report.AddPassed("Mixed inbound", $"127.0.0.1:{Settings.ProxyMixedPort} is available.");
                }
                catch (Exception ex)
                {
                    report.AddFailed("Mixed inbound", ex.Message);
                }
            }
            else
            {
                report.AddSkipped("Mixed inbound", "Current capture mode is TUN.");
            }
        }

        AppendRuleSetCheck(report, "CN domain rule-set", _paths.CnDomainRuleSetPath);
        AppendRuleSetCheck(report, "CN IP rule-set", _paths.CnIpRuleSetPath);

        _appLogger.Info(report.HasFailures ? "Self-check finished with failures." : "Self-check passed.");
        return report;
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        try
        {
            _processManager.Exited -= ProcessManagerOnExited;
            await StopTrafficMonitorAsync();

            if (_processManager.IsRunning)
            {
                await _processManager.StopAsync(cancellationToken);
            }

            TryRestoreManagedSystemProxy();
            AppSessionStateCoordinator.MarkDisposed(_appSessionState, _pendingApplicationExitReason, DateTimeOffset.Now);
            Persist();
        }
        catch (Exception ex)
        {
            // Best effort shutdown only.
            _appLogger.Error("Shutdown cleanup failed.", ex);
        }
        finally
        {
            _shutdownCompleted = true;
        }
    }

    public void Dispose()
    {
        if (!_shutdownCompleted)
        {
            _processManager.Exited -= ProcessManagerOnExited;
            StopTrafficMonitorAsync().GetAwaiter().GetResult();
            try
            {
                if (_processManager.IsRunning)
                {
                    _processManager.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Best effort shutdown only.
            }

            TryRestoreManagedSystemProxy();
            AppSessionStateCoordinator.MarkDisposed(_appSessionState, _pendingApplicationExitReason, DateTimeOffset.Now);
            Persist();
            _shutdownCompleted = true;
        }

        _subscriptionImportService.Dispose();
        _clashApiClient.Dispose();
        _ruleSetUpdateService.Dispose();
        _processManager.Dispose();
        _appLogger.Dispose();
    }

    private async Task PersistAndRestartIfRunningAsync(CancellationToken cancellationToken)
    {
        Persist();

        if (_processManager.IsRunning)
        {
            await ConnectAsync(cancellationToken);
        }
    }

    private async Task PersistAndHotApplyIfRunningAsync(Func<CancellationToken, Task> hotApply, CancellationToken cancellationToken)
    {
        Persist();

        if (!_processManager.IsRunning)
        {
            return;
        }

        try
        {
            await hotApply(cancellationToken);
        }
        catch (Exception hotApplyException)
        {
            try
            {
                await ConnectAsync(cancellationToken);
            }
            catch (Exception restartException)
            {
                throw new InvalidOperationException(
                    "Failed to apply settings through Clash API and failed to restart sing-box.",
                    new AggregateException(hotApplyException, restartException));
            }
        }
    }

    private void Persist()
    {
        _settingsStore.Save(Settings);
        _appStateStore.Save(_appSessionState);
        _nodesStore.Save(_nodes);
        _subscriptionsStore.Save(_subscriptions);
    }

    private void EnsureSelectedNode()
    {
        if (_nodes.Count == 0)
        {
            Settings.SelectedNodeId = null;
            return;
        }

        var current = _nodes.FirstOrDefault(node => node.Id == Settings.SelectedNodeId);
        if (current is not null && current.Enabled)
        {
            return;
        }

        var fallback = _nodes
            .Where(node => node.Enabled)
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? _nodes
                .OrderBy(node => node.SortOrder)
                .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                .First();

        Settings.SelectedNodeId = fallback.Id;
    }

    private void NormalizeSortOrders()
    {
        var ordered = _nodes
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            ordered[index].SortOrder = index;
        }
    }

    private static void ValidateNode(NodeProfile node, string? existingNodeId)
    {
        node.Name = node.Name.Trim();
        node.Group = string.IsNullOrWhiteSpace(node.Group) ? "Default" : node.Group.Trim();
        node.Server = node.Server.Trim();
        node.Username = node.Username.Trim();
        node.Password = node.Password.Trim();
        node.TlsServerName = node.TlsServerName.Trim();
        node.Remark = node.Remark.Trim();

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            throw new InvalidOperationException("Node name is required.");
        }

        if (string.IsNullOrWhiteSpace(node.Server))
        {
            throw new InvalidOperationException("Server is required.");
        }

        if (node.ServerPort is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Server port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(node.Id) && existingNodeId is null)
        {
            node.Id = Guid.NewGuid().ToString("N");
        }
    }

    private static void ValidateSubscription(SubscriptionProfile subscription, string? existingSubscriptionId)
    {
        subscription.Name = subscription.Name.Trim();
        subscription.Url = subscription.Url.Trim();

        if (string.IsNullOrWhiteSpace(subscription.Name))
        {
            throw new InvalidOperationException("Subscription name is required.");
        }

        if (string.IsNullOrWhiteSpace(subscription.Url) || !Uri.TryCreate(subscription.Url, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Subscription URL is invalid.");
        }

        if (string.IsNullOrWhiteSpace(subscription.Id) && existingSubscriptionId is null)
        {
            subscription.Id = Guid.NewGuid().ToString("N");
        }
    }

    private static void ValidateGeneralSettings(AppSettings settings)
    {
        if (settings.ProxyMixedPort is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Mixed port must be between 1 and 65535.");
        }

        if (settings.ClashApiPort is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Clash API port must be between 1 and 65535.");
        }

        if (settings.ProxyMixedPort == settings.ClashApiPort)
        {
            throw new InvalidOperationException("Mixed port and Clash API port must be different.");
        }

        var normalizedLogLevel = settings.LogLevel.Trim().ToLowerInvariant();
        if (normalizedLogLevel is not ("debug" or "info" or "warn" or "error"))
        {
            throw new InvalidOperationException("Log level must be one of: debug, info, warn, error.");
        }

        settings.LogLevel = normalizedLogLevel;
    }

    private void ApplyGeneralSettings(AppSettings settings)
    {
        Settings.ProxyMixedPort = settings.ProxyMixedPort;
        Settings.ClashApiPort = settings.ClashApiPort;
        Settings.EnableAutoStart = settings.EnableAutoStart;
        Settings.EnableMinimizeToTray = settings.EnableMinimizeToTray;
        Settings.EnableTunStrictRoute = settings.EnableTunStrictRoute;
        Settings.LogLevel = settings.LogLevel;
    }

    private void ProcessManagerOnExited(object? sender, int? exitCode)
    {
        _ = StopTrafficMonitorAsync();

        if (_suppressProcessExitError || RuntimeState.CoreStatus == CoreStatus.Stopping || RuntimeState.CoreStatus == CoreStatus.Stopped)
        {
            UpdateRuntimeState(state =>
            {
                state.CoreStatus = CoreStatus.Stopped;
                state.ProcessId = null;
                state.CurrentRealNodeId = string.Empty;
                state.CurrentLatency = null;
                state.StatusDetail = "Disconnected";
                ApplyElevationSessionSummary(state);
                ResetTrafficState(state);
            });
            AppSessionStateCoordinator.MarkGracefulStop(_appSessionState, _preserveRestoreConnectionOnNextDisconnect);
            Persist();
            _appLogger.Info($"sing-box stopped. ExitCode={exitCode?.ToString() ?? "unknown"}.");
            return;
        }

        UpdateRuntimeState(state =>
        {
            state.CoreStatus = CoreStatus.Error;
            state.ProcessId = null;
            state.CurrentRealNodeId = string.Empty;
            state.CurrentLatency = null;
            state.LastError = $"sing-box exited unexpectedly with code {exitCode?.ToString() ?? "unknown"}";
            state.StatusDetail = "sing-box exited unexpectedly";
            ApplyElevationSessionSummary(state);
            ResetTrafficState(state);
        });
        AppSessionStateCoordinator.MarkUnexpectedTermination(_appSessionState);
        Persist();
        _appLogger.Error($"sing-box exited unexpectedly. ExitCode={exitCode?.ToString() ?? "unknown"}.");
    }

    private async Task ApplyClashApiStateAsync(CancellationToken cancellationToken)
    {
        var controller = GetClashApiController();

        UpdateRuntimeState(state => state.StatusDetail = "Waiting for Clash API");
        await _clashApiClient.WaitUntilAvailableAsync(controller, Settings.ClashApiSecret, cancellationToken);
        UpdateRuntimeState(state => state.StatusDetail = "Applying route mode");
        await ApplyRouteModeAsync(cancellationToken);
        UpdateRuntimeState(state => state.StatusDetail = "Applying manual node");
        await ApplySelectedNodeAsync(cancellationToken);
        UpdateRuntimeState(state => state.StatusDetail = "Applying node mode");
        await ApplyNodeModeAsync(cancellationToken);
    }

    private Task ApplyRouteModeAsync(CancellationToken cancellationToken)
    {
        var controller = GetClashApiController();
        return _clashApiClient.SetModeAsync(
            controller,
            Settings.ClashApiSecret,
            Settings.RouteMode.ToString().ToLowerInvariant(),
            cancellationToken);
    }

    private async Task ApplyRouteModeTransitionAsync(CancellationToken cancellationToken)
    {
        var controller = GetClashApiController();

        UpdateRuntimeState(state => state.StatusDetail = "Applying route mode");
        await _clashApiClient.SetModeAsync(
            controller,
            Settings.ClashApiSecret,
            Settings.RouteMode.ToString().ToLowerInvariant(),
            cancellationToken);

        UpdateRuntimeState(state => state.StatusDetail = "Refreshing active connections");
        await _clashApiClient.CloseAllConnectionsAsync(controller, Settings.ClashApiSecret, cancellationToken);

        UpdateRuntimeState(state =>
        {
            if (state.CoreStatus == CoreStatus.Running)
            {
                state.StatusDetail = "Connected";
            }
        });
    }

    private async Task ApplySelectedNodeAsync(CancellationToken cancellationToken)
    {
        var selectedNode = SelectedNode;
        if (selectedNode is null || !selectedNode.Enabled || !_nodes.Any(node => node.Enabled))
        {
            UpdateRuntimeState(state => state.CurrentRealNodeId = string.Empty);
            return;
        }

        var controller = GetClashApiController();

        await _clashApiClient.SelectOutboundAsync(
            controller,
            Settings.ClashApiSecret,
            SingBoxTags.ManualSelector,
            SingBoxTags.GetNodeTag(selectedNode),
            cancellationToken);

        if (Settings.NodeMode == NodeMode.Manual)
        {
            await RefreshCurrentRealNodeStateAsync(cancellationToken);
            RefreshCurrentLatency();
        }
    }

    private async Task ApplyNodeModeAsync(CancellationToken cancellationToken)
    {
        var enabledNodes = _nodes.Any(node => node.Enabled);
        if (!enabledNodes)
        {
            UpdateRuntimeState(state => state.CurrentRealNodeId = string.Empty);
            return;
        }

        var controller = GetClashApiController();
        var nodeModeTag = Settings.NodeMode == NodeMode.Auto ? SingBoxTags.AutoSelector : SingBoxTags.ManualSelector;

        await _clashApiClient.SelectOutboundAsync(
            controller,
            Settings.ClashApiSecret,
            SingBoxTags.ProxySelector,
            nodeModeTag,
            cancellationToken);

        await RefreshCurrentRealNodeStateAsync(cancellationToken);
        RefreshCurrentLatency();
    }

    private string GetClashApiController() => _paths.CreateBuildContext(Settings.ClashApiPort).ClashApiController;

    private async Task RefreshSubscriptionInternalAsync(SubscriptionProfile subscription, CancellationToken cancellationToken)
    {
        if (!subscription.Enabled)
        {
            throw new InvalidOperationException("Subscription is disabled.");
        }

        try
        {
            var nodes = await DownloadSubscriptionNodesAsync(subscription, cancellationToken);
            ReplaceNodesForSubscription(subscription.Id, nodes);
            await PersistAndRestartIfRunningAsync(cancellationToken);
        }
        catch
        {
            Persist();
            throw;
        }
    }

    private async Task<IReadOnlyList<NodeProfile>> DownloadSubscriptionNodesAsync(SubscriptionProfile subscription, CancellationToken cancellationToken)
    {
        try
        {
            var importedNodes = await _subscriptionImportService.DownloadNodesAsync(subscription, cancellationToken);
            subscription.ImportedNodeCount = importedNodes.Count;
            subscription.LastUpdated = DateTimeOffset.Now;
            subscription.LastError = string.Empty;
            return importedNodes;
        }
        catch (Exception ex)
        {
            subscription.LastError = ex.Message;
            throw;
        }
    }

    private void ReplaceNodesForSubscription(string subscriptionId, IReadOnlyList<NodeProfile> importedNodes)
    {
        var removedNodeIds = _nodes
            .Where(node => string.Equals(node.SubscriptionId, subscriptionId, StringComparison.Ordinal))
            .Select(node => node.Id)
            .ToArray();

        foreach (var removedNodeId in removedNodeIds)
        {
            _nodeLatencies.Remove(removedNodeId);
            _nodeLatencyErrors.Remove(removedNodeId);
        }

        _nodes.RemoveAll(node => string.Equals(node.SubscriptionId, subscriptionId, StringComparison.Ordinal));

        var baseSortOrder = _nodes.Count == 0 ? 0 : _nodes.Max(node => node.SortOrder) + 1;
        for (var index = 0; index < importedNodes.Count; index++)
        {
            var importedNode = CloneNode(importedNodes[index]);
            importedNode.Id = string.IsNullOrWhiteSpace(importedNode.Id) ? Guid.NewGuid().ToString("N") : importedNode.Id;
            importedNode.SubscriptionId = subscriptionId;
            importedNode.SortOrder = baseSortOrder + index;
            _nodes.Add(importedNode);
        }

        NormalizeSortOrders();
        EnsureSelectedNode();
        RefreshCurrentLatency();
    }

    private async Task<int> TestNodeLatencyCoreAsync(NodeProfile node, CancellationToken cancellationToken)
    {
        EnsureLatencyTestReady();

        if (!node.Enabled)
        {
            throw new InvalidOperationException("Only enabled nodes can be tested.");
        }

        var controller = GetClashApiController();
        var nodeTag = SingBoxTags.GetNodeTag(node);

        try
        {
            await _clashApiClient.WaitUntilAvailableAsync(controller, Settings.ClashApiSecret, cancellationToken);
            var delay = await _clashApiClient.TestProxyDelayAsync(
                controller,
                Settings.ClashApiSecret,
                nodeTag,
                DefaultDelayTestUrl,
                DefaultDelayTimeoutMs,
                cancellationToken);

            _nodeLatencies[node.Id] = delay;
            _nodeLatencyErrors.Remove(node.Id);

            if (Settings.NodeMode == NodeMode.Manual && string.Equals(Settings.SelectedNodeId, node.Id, StringComparison.Ordinal))
            {
                UpdateRuntimeState(state => state.CurrentLatency = delay);
            }

            return delay;
        }
        catch (Exception ex)
        {
            _nodeLatencies.Remove(node.Id);
            _nodeLatencyErrors[node.Id] = BuildLatencyErrorLabel(ex);

            if (Settings.NodeMode == NodeMode.Manual && string.Equals(Settings.SelectedNodeId, node.Id, StringComparison.Ordinal))
            {
                UpdateRuntimeState(state => state.CurrentLatency = null);
            }

            throw;
        }
    }

    private void EnsureLatencyTestReady()
    {
        if (!_processManager.IsRunning)
        {
            throw new InvalidOperationException("Connect first before running node latency tests.");
        }

        if (!_nodes.Any(node => node.Enabled))
        {
            throw new InvalidOperationException("No enabled nodes are available for testing.");
        }
    }

    private void RefreshCurrentLatency()
    {
        var targetNodeId = Settings.NodeMode == NodeMode.Manual
            ? Settings.SelectedNodeId
            : RuntimeState.CurrentRealNodeId;

        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            UpdateRuntimeState(state => state.CurrentLatency = null);
            return;
        }

        UpdateRuntimeState(state => state.CurrentLatency = _nodeLatencies.GetValueOrDefault(targetNodeId));
    }

    private async Task TryRefreshCurrentRealNodeStateAsync(CancellationToken cancellationToken)
    {
        if (Settings.NodeMode != NodeMode.Auto || !_processManager.IsRunning)
        {
            return;
        }

        try
        {
            await RefreshCurrentRealNodeStateAsync(cancellationToken);
            RefreshCurrentLatency();
        }
        catch
        {
            // Keep traffic monitoring available even if selector inspection fails.
        }
    }

    private async Task RefreshCurrentRealNodeStateAsync(CancellationToken cancellationToken)
    {
        var currentRealNodeId = Settings.NodeMode == NodeMode.Manual
            ? Settings.SelectedNodeId ?? string.Empty
            : await ResolveCurrentRealNodeIdAsync(cancellationToken);

        UpdateRuntimeState(state => state.CurrentRealNodeId = currentRealNodeId);
    }

    private async Task<string> ResolveCurrentRealNodeIdAsync(CancellationToken cancellationToken)
    {
        if (!_nodes.Any(node => node.Enabled))
        {
            return string.Empty;
        }

        var controller = GetClashApiController();
        var proxySelection = await _clashApiClient.GetSelectedOutboundAsync(controller, Settings.ClashApiSecret, SingBoxTags.ProxySelector, cancellationToken);
        var manualSelection = await _clashApiClient.GetSelectedOutboundAsync(controller, Settings.ClashApiSecret, SingBoxTags.ManualSelector, cancellationToken);
        var autoSelection = await _clashApiClient.GetSelectedOutboundAsync(controller, Settings.ClashApiSecret, SingBoxTags.AutoSelector, cancellationToken);

        return ProxySelectionResolver.ResolveCurrentRealNodeId(proxySelection, manualSelection, autoSelection, _nodes);
    }

    private string GetCurrentNodeDisplayName()
    {
        var currentRealNode = _nodes.FirstOrDefault(node => string.Equals(node.Id, RuntimeState.CurrentRealNodeId, StringComparison.Ordinal));
        if (currentRealNode is not null)
        {
            return Settings.NodeMode == NodeMode.Auto ? $"Auto -> {currentRealNode.Name}" : currentRealNode.Name;
        }

        if (SelectedNode is not null)
        {
            return Settings.NodeMode == NodeMode.Auto ? $"Auto -> {SelectedNode.Name}" : SelectedNode.Name;
        }

        return Settings.NodeMode == NodeMode.Auto ? "Auto" : "-";
    }

    private static string BuildLatencyErrorLabel(Exception ex)
    {
        return ex.Message.Contains("504", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            ? "Timeout"
            : "Error";
    }

    private void SyncAutoStartRegistration()
    {
        try
        {
            _startupManager.SetEnabled(GetCurrentExecutablePath(), Settings.EnableAutoStart);
        }
        catch (Exception ex)
        {
            _appLogger.Error("Failed to sync auto-start registration.", ex);
            throw;
        }
    }

    private void ApplySystemProxyState()
    {
        if (Settings.CaptureMode == CaptureMode.Proxy)
        {
            CaptureSystemProxySnapshotIfNeeded();
            _systemProxyManager.EnableLoopbackProxy(Settings.ProxyMixedPort);
            return;
        }

        TryRestoreManagedSystemProxy();
    }

    private async Task ValidateSystemProxyStateAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken);

        if (Settings.CaptureMode == CaptureMode.Proxy)
        {
            if (_systemProxyManager.IsManagedProxyEnabled(Settings.ProxyMixedPort))
            {
                return;
            }

            var currentProxyServer = _systemProxyManager.GetProxyServer() ?? "disabled";
            throw new InvalidOperationException(
                $"Windows system proxy is not pointing to 127.0.0.1:{Settings.ProxyMixedPort}. Current value: {currentProxyServer}. Another proxy client may be overriding it.");
        }

        if (!_systemProxyManager.IsProxyEnabled())
        {
            return;
        }

        var conflictingProxyServer = _systemProxyManager.GetProxyServer() ?? "unknown";
        throw new InvalidOperationException(
            $"Windows system proxy is still enabled ({conflictingProxyServer}). Disable the other proxy client before using TUN mode.");
    }

    private void RecoverSystemProxyOnLaunch()
    {
        if (TryRestoreManagedSystemProxy())
        {
            return;
        }

        try
        {
            if (_systemProxyManager.DisableManagedProxyIfCurrent(Settings.ProxyMixedPort))
            {
                _appLogger.Info($"Cleared stale EasyNaive system proxy at 127.0.0.1:{Settings.ProxyMixedPort}.");
            }
        }
        catch (Exception ex)
        {
            _appLogger.Error("Failed to recover Windows system proxy on launch.", ex);
        }
    }

    private void CaptureSystemProxySnapshotIfNeeded()
    {
        if (_appSessionState.SystemProxySnapshot is not null)
        {
            return;
        }

        if (_systemProxyManager.IsManagedProxyEnabled(Settings.ProxyMixedPort))
        {
            return;
        }

        _appSessionState.SystemProxySnapshot = new SystemProxySnapshot
        {
            ProxyEnabled = _systemProxyManager.IsProxyEnabled(),
            ProxyServerExists = _systemProxyManager.ProxyServerExists(),
            ProxyServer = _systemProxyManager.GetProxyServer() ?? string.Empty,
            ManagedPort = Settings.ProxyMixedPort,
            CapturedAt = DateTimeOffset.Now
        };
        Persist();
        _appLogger.Info("Captured Windows system proxy snapshot before enabling EasyNaive proxy.");
    }

    private bool TryRestoreManagedSystemProxy()
    {
        try
        {
            var snapshot = _appSessionState.SystemProxySnapshot;
            if (snapshot is not null)
            {
                var currentIsManaged =
                    (snapshot.ManagedPort > 0 && _systemProxyManager.IsManagedProxyEnabled(snapshot.ManagedPort)) ||
                    _systemProxyManager.IsManagedProxyEnabled(Settings.ProxyMixedPort);

                if (currentIsManaged)
                {
                    _systemProxyManager.RestoreProxyState(
                        snapshot.ProxyEnabled,
                        snapshot.ProxyServerExists,
                        snapshot.ProxyServer);
                    _appLogger.Info("Restored Windows system proxy from EasyNaive snapshot.");
                }
                else
                {
                    _appLogger.Info("Skipped Windows system proxy restore because the current proxy is no longer managed by EasyNaive.");
                }

                _appSessionState.SystemProxySnapshot = null;
                Persist();
                return currentIsManaged;
            }

            if (_systemProxyManager.DisableManagedProxyIfCurrent(Settings.ProxyMixedPort))
            {
                _appLogger.Info($"Cleared EasyNaive system proxy at 127.0.0.1:{Settings.ProxyMixedPort}.");
                return true;
            }
        }
        catch (Exception ex)
        {
            _appLogger.Error("Failed to restore Windows system proxy.", ex);
        }

        return false;
    }

    private void StartTrafficMonitor()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        _trafficMonitorCancellationTokenSource = cancellationTokenSource;
        _trafficMonitorTask = MonitorTrafficAsync(cancellationTokenSource.Token);
    }

    private async Task StopTrafficMonitorAsync()
    {
        var cancellationTokenSource = _trafficMonitorCancellationTokenSource;
        var monitorTask = _trafficMonitorTask;

        _trafficMonitorCancellationTokenSource = null;
        _trafficMonitorTask = null;

        if (cancellationTokenSource is not null)
        {
            cancellationTokenSource.Cancel();
        }

        if (monitorTask is not null)
        {
            try
            {
                await monitorTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown/reconnect.
            }
            catch
            {
                // Best effort cleanup only.
            }
        }

        cancellationTokenSource?.Dispose();
        UpdateRuntimeState(ResetTrafficState);
    }

    private async Task MonitorTrafficAsync(CancellationToken cancellationToken)
    {
        var controller = GetClashApiController();
        ClashTrafficSnapshot? previousSnapshot = null;
        DateTimeOffset? previousAt = null;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var snapshot = await _clashApiClient.GetTrafficSnapshotAsync(controller, Settings.ClashApiSecret, cancellationToken);
                var sampledAt = DateTimeOffset.UtcNow;
                ApplyTrafficSnapshot(previousSnapshot, snapshot, previousAt, sampledAt);
                await TryRefreshCurrentRealNodeStateAsync(cancellationToken);

                previousSnapshot = snapshot;
                previousAt = sampledAt;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                UpdateRuntimeState(state =>
                {
                    state.DownloadRateBytesPerSecond = 0;
                    state.UploadRateBytesPerSecond = 0;
                    state.ActiveConnections = 0;
                });
            }

            await timer.WaitForNextTickAsync(cancellationToken);
        }
    }

    private void ApplyTrafficSnapshot(
        ClashTrafficSnapshot? previousSnapshot,
        ClashTrafficSnapshot currentSnapshot,
        DateTimeOffset? previousAt,
        DateTimeOffset currentAt)
    {
        var downloadRate = 0L;
        var uploadRate = 0L;

        if (previousSnapshot is not null && previousAt is not null)
        {
            var elapsedSeconds = Math.Max((currentAt - previousAt.Value).TotalSeconds, 0.001d);
            var downloadDelta = Math.Max(0L, currentSnapshot.DownloadTotalBytes - previousSnapshot.DownloadTotalBytes);
            var uploadDelta = Math.Max(0L, currentSnapshot.UploadTotalBytes - previousSnapshot.UploadTotalBytes);

            downloadRate = (long)Math.Round(downloadDelta / elapsedSeconds);
            uploadRate = (long)Math.Round(uploadDelta / elapsedSeconds);
        }

        UpdateRuntimeState(state =>
        {
            state.DownloadTotalBytes = currentSnapshot.DownloadTotalBytes;
            state.UploadTotalBytes = currentSnapshot.UploadTotalBytes;
            state.DownloadRateBytesPerSecond = downloadRate;
            state.UploadRateBytesPerSecond = uploadRate;
            state.ActiveConnections = currentSnapshot.ActiveConnections;
        });
    }

    private static void ResetTrafficState(RuntimeState state)
    {
        state.DownloadTotalBytes = 0;
        state.UploadTotalBytes = 0;
        state.DownloadRateBytesPerSecond = 0;
        state.UploadRateBytesPerSecond = 0;
        state.ActiveConnections = 0;
    }

    private static NodeProfile CloneNode(NodeProfile source)
    {
        return new NodeProfile
        {
            Id = source.Id,
            SubscriptionId = source.SubscriptionId,
            Name = source.Name,
            Group = source.Group,
            Server = source.Server,
            ServerPort = source.ServerPort,
            Username = source.Username,
            Password = source.Password,
            TlsServerName = source.TlsServerName,
            UseQuic = source.UseQuic,
            UseUdpOverTcp = source.UseUdpOverTcp,
            Enabled = source.Enabled,
            SortOrder = source.SortOrder,
            Remark = source.Remark
        };
    }

    private static SubscriptionProfile CloneSubscription(SubscriptionProfile source)
    {
        return new SubscriptionProfile
        {
            Id = source.Id,
            Name = source.Name,
            Url = source.Url,
            Enabled = source.Enabled,
            ImportedNodeCount = source.ImportedNodeCount,
            LastUpdated = source.LastUpdated,
            LastError = source.LastError
        };
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            CaptureMode = source.CaptureMode,
            RouteMode = source.RouteMode,
            NodeMode = source.NodeMode,
            SelectedNodeId = source.SelectedNodeId,
            ProxyMixedPort = source.ProxyMixedPort,
            ClashApiPort = source.ClashApiPort,
            ClashApiSecret = source.ClashApiSecret,
            EnableAutoStart = source.EnableAutoStart,
            EnableMinimizeToTray = source.EnableMinimizeToTray,
            EnableTunStrictRoute = source.EnableTunStrictRoute,
            LogLevel = source.LogLevel
        };
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private void OpenDirectory(string path)
    {
        _shellManager.OpenDirectory(path);
    }

    private static string GetCurrentExecutablePath()
    {
        return Environment.ProcessPath ?? throw new InvalidOperationException("Unable to resolve the current executable path.");
    }

    private static async Task VerifyLoopbackPortAsync(string host, int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
    }

    private static void EnsureLoopbackPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"127.0.0.1:{port} is already in use.", ex);
        }
        finally
        {
            listener?.Stop();
        }
    }

    private static void AppendRuleSetCheck(SelfCheckReport report, string name, string path)
    {
        if (File.Exists(path))
        {
            report.AddPassed(name, path);
            return;
        }

        report.AddSkipped(name, $"Not found: {path}");
    }

    private void AppendElevationSessionCheck(SelfCheckReport report)
    {
        var summary = ElevationSessionStore.GetSummary(_paths.ElevationSessionPath);
        if (!summary.Exists)
        {
            report.AddSkipped("Elevation session", "No elevation session file.");
            return;
        }

        if (!summary.IsReadable)
        {
            report.AddFailed("Elevation session", summary.Detail);
            return;
        }

        if (summary.Status == ElevationSessionStatus.Running && !summary.IsSingBoxProcessAlive)
        {
            report.AddFailed("Elevation session", "Stale running session: " + summary.Detail);
            return;
        }

        if (summary.Status == ElevationSessionStatus.Failed)
        {
            report.AddFailed("Elevation session", summary.Detail);
            return;
        }

        report.AddPassed("Elevation session", summary.Detail);
    }

    private void ApplyElevationSessionSummary(RuntimeState state)
    {
        if (Settings.CaptureMode != CaptureMode.Tun)
        {
            state.ElevationHelperProcessId = null;
            state.ElevationSingBoxProcessId = null;
            state.ElevationStatusDetail = string.Empty;
            return;
        }

        var summary = ElevationSessionStore.GetSummary(_paths.ElevationSessionPath);
        state.ElevationHelperProcessId = summary.HelperProcessId > 0 ? summary.HelperProcessId : null;
        state.ElevationSingBoxProcessId = summary.SingBoxProcessId > 0 ? summary.SingBoxProcessId : null;
        state.ElevationStatusDetail = summary.Exists ? summary.Detail : string.Empty;
    }

    private void UpdateRuntimeState(Action<RuntimeState> apply)
    {
        apply(RuntimeState);
        RuntimeStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
