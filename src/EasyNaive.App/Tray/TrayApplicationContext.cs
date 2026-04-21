using System.Drawing;
using System.Windows.Forms;
using EasyNaive.App.Forms;
using EasyNaive.App.Presentation;
using EasyNaive.Core.Enums;

namespace EasyNaive.App.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly CoreController _controller;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;
    private readonly Icon _trayStoppedIcon;
    private readonly Icon _trayConnectedIcon;
    private readonly Icon _trayErrorIcon;
    private CoreStatus _lastKnownStatus;
    private bool _exitRequested;
    private bool _startupRecoveryScheduled;

    public TrayApplicationContext(CoreController controller)
    {
        _controller = controller;
        _controller.RuntimeStateChanged += ControllerOnRuntimeStateChanged;
        _mainForm = new MainForm(controller);
        _mainForm.FormClosing += MainFormOnFormClosing;
        _mainForm.Resize += MainFormOnResize;
        _trayIcon = AppIcons.CreateApplicationIcon();
        _trayStoppedIcon = AppIcons.CreateTrayStoppedIcon();
        _trayConnectedIcon = AppIcons.CreateTrayConnectedIcon();
        _trayErrorIcon = AppIcons.CreateTrayErrorIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = BuildTrayIcon(),
            Text = BuildTrayText(),
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _lastKnownStatus = _controller.RuntimeState.CoreStatus;

        ShowMainWindow();
        ScheduleStartupRecovery();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.RuntimeStateChanged -= ControllerOnRuntimeStateChanged;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
            _trayStoppedIcon.Dispose();
            _trayConnectedIcon.Dispose();
            _trayErrorIcon.Dispose();
            _mainForm.Dispose();
            _controller.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Connect", null, async (_, _) => await ExecuteAsync(_controller.ConnectAsync));
        menu.Items.Add("Disconnect", null, async (_, _) => await ExecuteAsync(_controller.DisconnectAsync));
        menu.Items.Add("Restart Core", null, async (_, _) => await ExecuteAsync(_controller.RestartCoreAsync));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Settings", null, async (_, _) => await OpenSettingsAsync());
        menu.Items.Add("Subscriptions", null, (_, _) => OpenSubscriptions());
        menu.Items.Add("Update Rule-Sets", null, async (_, _) => await UpdateRuleSetsAsync());
        menu.Items.Add("Run Self Check", null, async (_, _) => await RunSelfCheckAsync());
        menu.Items.Add("Open Data Folder", null, (_, _) => _controller.OpenDataDirectory());
        menu.Items.Add("Open Logs", null, (_, _) => _controller.OpenLogsDirectory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(BuildNodesMenu());
        menu.Items.Add(BuildCaptureModeMenu());
        menu.Items.Add(BuildRouteModeMenu());
        menu.Items.Add(BuildNodeModeMenu());
        menu.Items.Add(BuildAutoStartMenuItem());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitApplicationAsync());

        return menu;
    }

    private ToolStripMenuItem BuildNodesMenu()
    {
        var root = new ToolStripMenuItem("Manual Node");

        if (_controller.Nodes.Count == 0)
        {
            root.DropDownItems.Add(new ToolStripMenuItem("No nodes configured")
            {
                Enabled = false
            });

            return root;
        }

        foreach (var node in _controller.Nodes)
        {
            var menuItem = new ToolStripMenuItem(node.Name)
            {
                Checked = string.Equals(node.Id, _controller.Settings.SelectedNodeId, StringComparison.Ordinal),
                Enabled = node.Enabled
            };

            menuItem.Click += async (_, _) => await ExecuteAsync(ct => _controller.SetSelectedNodeAsync(node.Id, ct));
            root.DropDownItems.Add(menuItem);
        }

        return root;
    }

    private ToolStripMenuItem BuildCaptureModeMenu()
    {
        var root = new ToolStripMenuItem("Capture Mode");

        root.DropDownItems.Add(CreateModeItem("Proxy", _controller.Settings.CaptureMode == CaptureMode.Proxy, ct =>
            _controller.SetCaptureModeAsync(CaptureMode.Proxy, ct)));

        root.DropDownItems.Add(CreateModeItem("Tun", _controller.Settings.CaptureMode == CaptureMode.Tun, ct =>
            _controller.SetCaptureModeAsync(CaptureMode.Tun, ct)));

        return root;
    }

    private ToolStripMenuItem BuildRouteModeMenu()
    {
        var root = new ToolStripMenuItem("Route Mode");

        root.DropDownItems.Add(CreateModeItem("Rule", _controller.Settings.RouteMode == RouteMode.Rule, ct =>
            _controller.SetRouteModeAsync(RouteMode.Rule, ct)));

        root.DropDownItems.Add(CreateModeItem("Global", _controller.Settings.RouteMode == RouteMode.Global, ct =>
            _controller.SetRouteModeAsync(RouteMode.Global, ct)));

        root.DropDownItems.Add(CreateModeItem("Direct", _controller.Settings.RouteMode == RouteMode.Direct, ct =>
            _controller.SetRouteModeAsync(RouteMode.Direct, ct)));

        return root;
    }

    private ToolStripMenuItem BuildNodeModeMenu()
    {
        var root = new ToolStripMenuItem("Node Mode");

        root.DropDownItems.Add(CreateModeItem("Manual", _controller.Settings.NodeMode == NodeMode.Manual, ct =>
            _controller.SetNodeModeAsync(NodeMode.Manual, ct)));

        root.DropDownItems.Add(CreateModeItem("Auto", _controller.Settings.NodeMode == NodeMode.Auto, ct =>
            _controller.SetNodeModeAsync(NodeMode.Auto, ct)));

        return root;
    }

    private ToolStripMenuItem CreateModeItem(string text, bool isChecked, Func<CancellationToken, Task> onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            Checked = isChecked
        };
        item.Click += async (_, _) => await ExecuteAsync(onClick);

        return item;
    }

    private ToolStripMenuItem BuildAutoStartMenuItem()
    {
        var item = new ToolStripMenuItem("Launch at Startup")
        {
            Checked = _controller.Settings.EnableAutoStart
        };
        item.Click += async (_, _) => await ExecuteAsync(ct => _controller.SetAutoStartAsync(!_controller.Settings.EnableAutoStart, ct));
        return item;
    }

    private void MainFormOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        _mainForm.Hide();
    }

    private void MainFormOnResize(object? sender, EventArgs e)
    {
        if (_exitRequested || !_controller.Settings.EnableMinimizeToTray)
        {
            return;
        }

        if (_mainForm.WindowState == FormWindowState.Minimized)
        {
            _mainForm.Hide();
        }
    }

    private void ShowMainWindow()
    {
        if (!_mainForm.Visible)
        {
            _mainForm.Show();
        }

        if (_mainForm.WindowState == FormWindowState.Minimized)
        {
            _mainForm.WindowState = FormWindowState.Normal;
        }

        _mainForm.BringToFront();
        _mainForm.Activate();
    }

    private void ScheduleStartupRecovery()
    {
        if (_startupRecoveryScheduled || _mainForm.IsDisposed)
        {
            return;
        }

        _startupRecoveryScheduled = true;
        _mainForm.BeginInvoke(new MethodInvoker(() => _ = RunStartupRecoveryAsync()));
    }

    private void RefreshUi()
    {
        var previousMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildMenu();
        _notifyIcon.Text = BuildTrayText();
        _notifyIcon.Icon = BuildTrayIcon();
        previousMenu?.Dispose();
        _mainForm.RefreshView();
    }

    private void RefreshRuntimeUi()
    {
        _notifyIcon.Text = BuildTrayText();
        _notifyIcon.Icon = BuildTrayIcon();
        _mainForm.RefreshSummary();
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(5000, "EasyNaive", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            RefreshUi();
        }
    }

    private async Task RunStartupRecoveryAsync()
    {
        if (_controller.HadUncleanShutdown)
        {
            var message = _controller.PreviousExitReason == SessionExitReason.UnexpectedTermination
                ? "The previous session ended unexpectedly."
                : "The previous session did not shut down cleanly.";
            _notifyIcon.ShowBalloonTip(4000, "EasyNaive", message, ToolTipIcon.Warning);
        }

        if (!_controller.ShouldRestoreConnectionOnLaunch)
        {
            if (_controller.PreviousExitReason == SessionExitReason.RecoveryFailed && !string.IsNullOrWhiteSpace(_controller.PreviousRecoveryError))
            {
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "EasyNaive",
                    $"Automatic recovery was disabled after a previous failure: {_controller.PreviousRecoveryError}",
                    ToolTipIcon.Warning);
            }

            RefreshUi();
            return;
        }

        var startupMessage = _controller.PreviousExitReason switch
        {
            SessionExitReason.ApplicationExitConnected => "Restoring the previous connected session.",
            SessionExitReason.UnexpectedTermination => "Recovering the previous session after an unexpected shutdown.",
            _ => "Restoring the previous connected session."
        };
        _notifyIcon.ShowBalloonTip(2500, "EasyNaive", startupMessage, ToolTipIcon.Info);

        try
        {
            await _controller.ConnectAsync(CancellationToken.None);
            _controller.MarkStartupRecoverySucceeded();
        }
        catch (Exception ex)
        {
            _controller.MarkStartupRecoveryFailed(ex.Message);
            _notifyIcon.ShowBalloonTip(
                5000,
                "EasyNaive",
                "Automatic recovery failed. EasyNaive will stay disconnected until you connect manually.",
                ToolTipIcon.Warning);

            if (_mainForm.Visible)
            {
                MessageBox.Show(
                    _mainForm,
                    $"Automatic recovery failed and has been disabled for the next launch.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "EasyNaive Recovery Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            RefreshUi();
        }
    }

    private void OpenSubscriptions()
    {
        using var dialog = new SubscriptionManagerForm(_controller);
        dialog.ShowDialog(_mainForm);
        RefreshUi();
    }

    private async Task OpenSettingsAsync()
    {
        using var dialog = new SettingsForm(_controller.CreateSettingsDraft());
        if (dialog.ShowDialog(_mainForm) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.UpdateGeneralSettingsAsync(dialog.Settings, ct));
    }

    private async Task UpdateRuleSetsAsync()
    {
        try
        {
            var summary = await _controller.UpdateRuleSetsAsync(CancellationToken.None);
            _notifyIcon.ShowBalloonTip(3000, "EasyNaive", "Rule-sets updated.", ToolTipIcon.Info);

            if (_mainForm.Visible)
            {
                MessageBox.Show(
                    _mainForm,
                    summary,
                    "EasyNaive Rule-Sets",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(5000, "EasyNaive", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            RefreshUi();
        }
    }

    private async Task RunSelfCheckAsync()
    {
        try
        {
            var report = await _controller.RunSelfCheckAsync(CancellationToken.None);
            _notifyIcon.ShowBalloonTip(
                report.HasFailures ? 5000 : 2500,
                "EasyNaive",
                report.HasFailures ? "Self-check found issues." : "Self-check passed.",
                report.HasFailures ? ToolTipIcon.Warning : ToolTipIcon.Info);

            if (_mainForm.Visible)
            {
                MessageBox.Show(
                    _mainForm,
                    report.ToDisplayText(),
                    report.HasFailures ? "EasyNaive Self Check" : "EasyNaive Self Check Passed",
                    MessageBoxButtons.OK,
                    report.HasFailures ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(5000, "EasyNaive", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            RefreshUi();
        }
    }

    private string BuildTrayText()
    {
        const int maxLength = 63;
        var status = _controller.RuntimeState.CoreStatus.ToString();
        var detail = _controller.RuntimeState.CoreStatus == CoreStatus.Running
            ? $"D {TrafficFormatter.FormatRate(_controller.RuntimeState.DownloadRateBytesPerSecond)} U {TrafficFormatter.FormatRate(_controller.RuntimeState.UploadRateBytesPerSecond)}"
            : string.IsNullOrWhiteSpace(_controller.RuntimeState.StatusDetail)
                ? _controller.SelectedNode?.Name ?? "No node"
                : _controller.RuntimeState.StatusDetail;
        var text = $"EasyNaive - {status} - {detail}";
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private Icon BuildTrayIcon()
    {
        return _controller.RuntimeState.CoreStatus switch
        {
            CoreStatus.Running => _trayConnectedIcon,
            CoreStatus.Error => _trayErrorIcon,
            CoreStatus.Stopped => _trayStoppedIcon,
            _ => _trayIcon
        };
    }

    private void ControllerOnRuntimeStateChanged(object? sender, EventArgs e)
    {
        if (_mainForm.IsDisposed)
        {
            return;
        }

        if (_mainForm.InvokeRequired)
        {
            _mainForm.BeginInvoke(new MethodInvoker(HandleRuntimeStateChanged));
            return;
        }

        HandleRuntimeStateChanged();
    }

    private void HandleRuntimeStateChanged()
    {
        RefreshRuntimeUi();
        ShowStatusNotificationIfNeeded();
    }

    private void ShowStatusNotificationIfNeeded()
    {
        var currentStatus = _controller.RuntimeState.CoreStatus;
        if (currentStatus == _lastKnownStatus)
        {
            return;
        }

        _lastKnownStatus = currentStatus;

        switch (currentStatus)
        {
            case CoreStatus.Running:
                _notifyIcon.ShowBalloonTip(2500, "EasyNaive", "Connected.", ToolTipIcon.Info);
                break;
            case CoreStatus.Stopped:
                _notifyIcon.ShowBalloonTip(2500, "EasyNaive", "Disconnected.", ToolTipIcon.Info);
                break;
            case CoreStatus.Error:
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "EasyNaive",
                    string.IsNullOrWhiteSpace(_controller.RuntimeState.LastError) ? "Connection error." : _controller.RuntimeState.LastError,
                    ToolTipIcon.Error);
                break;
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;
        _controller.PrepareForApplicationExit();

        using var shutdownCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            if (_controller.RuntimeState.ProcessId is not null)
            {
                await _controller.DisconnectAsync(shutdownCancellationTokenSource.Token);
            }
        }
        catch
        {
            // Best effort shutdown to avoid leaving sing-box running in the background.
        }

        try
        {
            await _controller.ShutdownAsync(shutdownCancellationTokenSource.Token);
        }
        catch
        {
            // Best effort shutdown only. The app should still be allowed to exit.
        }

        _notifyIcon.Visible = false;
        if (!_mainForm.IsDisposed)
        {
            _mainForm.Close();
        }

        ExitThread();
    }
}
