using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EasyNaive.App.Diagnostics;
using EasyNaive.App.Importing;
using EasyNaive.App.Presentation;
using EasyNaive.App.Sharing;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Forms;

internal sealed class MainForm : Form, IMessageFilter
{
    private const int CsDropShadow = 0x00020000;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmRoundCorners = 2;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int HtCaption = 2;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int WmNcHitTest = 0x0084;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int WmLeftButtonDoubleClick = 0x0203;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmMouseMove = 0x0200;
    private const string AllGroupsFilterText = "All groups";

    private readonly CoreController _controller;
    private readonly WindowCaptionButtonStrip _windowButtonStrip;
    private readonly Panel _statusPanel;
    private readonly Label _statusLabel;
    private readonly Label _statusDetailLabel;
    private readonly Label _trafficLabel;
    private readonly Label _summaryLabel;
    private readonly PictureBox _statusLogoPictureBox;
    private readonly Label _healthMetricValueLabel;
    private readonly Label _nodesMetricValueLabel;
    private readonly Label _pidMetricValueLabel;
    private readonly Label _modeMetricValueLabel;
    private readonly SwitchButton _captureModeSwitchButton;
    private readonly ComboBox _routeModeComboBox;
    private readonly ComboBox _nodeModeComboBox;
    private readonly Button _connectButton;
    private readonly Button _disconnectButton;
    private readonly Button _restartCoreButton;
    private readonly Button _refreshButton;
    private readonly Button _openDataButton;
    private readonly Button _openLogsButton;
    private readonly Button _settingsButton;
    private readonly Button _updateRulesButton;
    private readonly Button _selfCheckButton;
    private readonly Button _subscriptionsButton;
    private readonly Button _importTextButton;
    private readonly Button _importClipboardButton;
    private readonly Button _addNodeButton;
    private readonly Button _editNodeButton;
    private readonly Button _deleteNodeButton;
    private readonly Button _useNodeButton;
    private readonly Button _testNodeButton;
    private readonly Button _testAllButton;
    private readonly Button _copyShareLinkButton;
    private readonly Button _exportNodesButton;
    private readonly DataGridView _nodesGrid;
    private readonly ContextMenuStrip _nodesContextMenu;
    private readonly SearchBox _nodeSearchTextBox;
    private readonly ComboBox _groupFilterComboBox;
    private readonly Label _nodeFilterSummaryLabel;
    private readonly TextBox _previewTextBox;
    private readonly CheckBox _autoStartCheckBox;
    private readonly Icon _windowIcon;
    private readonly Image _statusStoppedImage;
    private readonly Image _statusWaitingImage;
    private readonly Image _statusConnectedImage;
    private readonly Image _statusErrorImage;
    private int _hoveredNodeRowIndex = -1;
    private bool _suppressAutoStartChanged;
    private bool _suppressGroupFilterChanged;
    private bool _messageFilterRegistered;
    private bool _suppressModeSelectionChanged;

    public MainForm(CoreController controller)
    {
        _controller = controller;
        _controller.RuntimeStateChanged += ControllerOnRuntimeStateChanged;

        Text = "EasyNaive";
        _windowIcon = AppIcons.CreateApplicationIcon();
        _statusStoppedImage = AppIcons.CreateStatusStoppedImage();
        _statusWaitingImage = AppIcons.CreateStatusWaitingImage();
        _statusConnectedImage = AppIcons.CreateStatusConnectedImage();
        _statusErrorImage = AppIcons.CreateStatusErrorImage();
        Icon = _windowIcon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(1280, 760);
        MinimumSize = new Size(1100, 680);
        BackColor = ModernTheme.BackgroundBottom;
        Font = ModernTheme.BodyFont;
        DoubleBuffered = true;

        _windowButtonStrip = new WindowCaptionButtonStrip
        {
            Width = 128,
            Height = 38,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _windowButtonStrip.MinimizeClicked += (_, _) => WindowState = FormWindowState.Minimized;
        _windowButtonStrip.MaximizeClicked += (_, _) => ToggleWindowMaximized();
        _windowButtonStrip.CloseClicked += (_, _) => Close();
        SizeChanged += (_, _) => SyncWindowCaptionButtons();

        _statusPanel = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 136,
            Padding = new Padding(22, 18, 130, 16),
            StartColor = ModernTheme.BackgroundTop,
            EndColor = ModernTheme.BackgroundBottom
        };
        _statusPanel.Controls.Add(_windowButtonStrip);
        PositionWindowButtonPanel();
        _statusPanel.SizeChanged += (_, _) => PositionWindowButtonPanel();

        _summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Font = ModernTheme.BodyFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Font = ModernTheme.TitleFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusDetailLabel = new Label { Visible = false };

        _trafficLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Font = ModernTheme.SectionFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusLogoPictureBox = CreateStatusLogoPictureBox();
        _connectButton = CreateActionButton("Connect", async () => await ExecuteAsync(_controller.ConnectAsync));
        _disconnectButton = CreateActionButton("Disconnect", async () => await ExecuteAsync(_controller.DisconnectAsync));
        _restartCoreButton = CreateActionButton("Restart Core", async () => await ExecuteAsync(_controller.RestartCoreAsync));
        _openDataButton = CreateActionButton("Open Data Folder", () =>
        {
            _controller.OpenDataDirectory();
            return Task.CompletedTask;
        });
        _openLogsButton = CreateActionButton("Open Logs", OpenLogsAsync);
        _settingsButton = CreateActionButton("Settings", OpenSettingsAsync);
        _updateRulesButton = CreateActionButton("Update Rules", UpdateRulesAsync);
        _selfCheckButton = CreateActionButton("Self Check", RunSelfCheckAsync);
        _subscriptionsButton = CreateActionButton("Subscriptions", OpenSubscriptionsAsync);
        _importTextButton = CreateActionButton("Import Text", OpenTextImportAsync);
        _importClipboardButton = CreateActionButton("Import Clipboard", OpenClipboardImportAsync);

        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var statusMainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        statusMainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        statusMainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusMainPanel.Controls.Add(_statusLogoPictureBox, 0, 0);

        var statusTextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        statusTextPanel.Controls.Add(_statusLabel, 0, 0);
        statusTextPanel.Controls.Add(_trafficLabel, 0, 1);
        statusTextPanel.Controls.Add(_summaryLabel, 0, 2);
        _healthMetricValueLabel = new Label();
        _nodesMetricValueLabel = new Label();
        _pidMetricValueLabel = new Label();
        _modeMetricValueLabel = new Label();
        statusMainPanel.Controls.Add(statusTextPanel, 1, 0);

        var primaryActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        primaryActionPanel.Controls.Add(_connectButton);
        primaryActionPanel.Controls.Add(_disconnectButton);
        primaryActionPanel.Controls.Add(_restartCoreButton);

        var utilityActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 0),
            WrapContents = true,
            BackColor = Color.Transparent
        };
        utilityActionPanel.Controls.Add(CreateModeLabel("Tools"));
        utilityActionPanel.Controls.Add(_openDataButton);
        utilityActionPanel.Controls.Add(_openLogsButton);
        utilityActionPanel.Controls.Add(_settingsButton);
        utilityActionPanel.Controls.Add(_updateRulesButton);
        utilityActionPanel.Controls.Add(_selfCheckButton);

        statusLayout.Controls.Add(statusMainPanel, 0, 0);
        statusLayout.Controls.Add(primaryActionPanel, 1, 0);
        _statusPanel.Controls.Add(statusLayout);
        _windowButtonStrip.BringToFront();

        var commandPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(20, 10, 20, 8),
            BackColor = ModernTheme.BackgroundBottom
        };
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        commandPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var modePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            WrapContents = true,
            BackColor = Color.Transparent
        };
        modePanel.Controls.Add(CreateModeLabel("Capture"));
        _captureModeSwitchButton = new SwitchButton
        {
            OffText = "Proxy",
            OnText = "TUN",
            Margin = new Padding(4, 1, 12, 0),
            AccentColor = ModernTheme.Accent
        };
        _captureModeSwitchButton.CheckedChanged += CaptureModeSwitchButtonOnCheckedChanged;
        modePanel.Controls.Add(_captureModeSwitchButton);

        modePanel.Controls.Add(CreateModeLabel("Route"));
        _routeModeComboBox = CreateModeComboBox();
        _routeModeComboBox.Items.AddRange([RouteMode.Rule, RouteMode.Global, RouteMode.Direct]);
        _routeModeComboBox.SelectedIndexChanged += RouteModeComboBoxOnSelectedIndexChanged;
        modePanel.Controls.Add(_routeModeComboBox);

        modePanel.Controls.Add(CreateModeLabel("Node Mode"));
        _nodeModeComboBox = CreateModeComboBox();
        _nodeModeComboBox.Items.AddRange([NodeMode.Manual, NodeMode.Auto]);
        _nodeModeComboBox.SelectedIndexChanged += NodeModeComboBoxOnSelectedIndexChanged;
        modePanel.Controls.Add(_nodeModeComboBox);

        _autoStartCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Launch at startup",
            Checked = _controller.Settings.EnableAutoStart,
            Padding = new Padding(0, 6, 0, 0),
            ForeColor = ModernTheme.Text
        };
        _autoStartCheckBox.CheckedChanged += AutoStartCheckBoxOnCheckedChanged;

        var utilityPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            WrapContents = false,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        utilityPanel.Controls.Add(_autoStartCheckBox);

        commandPanel.Controls.Add(utilityActionPanel, 0, 0);
        commandPanel.Controls.Add(utilityPanel, 1, 0);

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(20, 6, 20, 6),
            BackColor = ModernTheme.BackgroundBottom
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modePanel.Margin = Padding.Empty;
        modePanel.Dock = DockStyle.Right;
        modePanel.Anchor = AnchorStyles.Right;
        footerPanel.Controls.Add(modePanel, 0, 0);

        var contentShell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            BackColor = ModernTheme.BackgroundBottom
        };
        contentShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentShell.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        contentShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var tabHeaderPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(18, 6, 18, 0),
            Margin = Padding.Empty,
            WrapContents = false,
            BackColor = ModernTheme.BackgroundBottom
        };
        var nodesTabButton = CreateContentTabButton("Nodes");
        var diagnosticsTabButton = CreateContentTabButton("Diagnostics");
        tabHeaderPanel.Controls.Add(nodesTabButton);
        tabHeaderPanel.Controls.Add(diagnosticsTabButton);

        var nodesPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = ModernTheme.BackgroundBottom,
            Padding = new Padding(18, 2, 18, 12)
        };
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var nodeToolbarPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Padding = new Padding(0, 5, 0, 3),
            Margin = Padding.Empty,
            WrapContents = true,
            BackColor = ModernTheme.BackgroundBottom
        };

        _addNodeButton = CreateActionButton("Add Node", AddNodeAsync);
        _editNodeButton = CreateActionButton("Edit", EditNodeAsync);
        _deleteNodeButton = CreateActionButton("Delete", DeleteNodeAsync);
        _useNodeButton = CreateActionButton("Use as Manual", UseSelectedNodeAsync);
        _testNodeButton = CreateActionButton("Test Selected", TestSelectedNodeAsync);
        _testAllButton = CreateActionButton("Test All", TestAllNodesAsync);
        _copyShareLinkButton = CreateActionButton("Copy Share Link", CopySelectedNodeShareLinksAsync);
        _exportNodesButton = CreateActionButton("Export Nodes", ExportSelectedNodesAsync);
        ApplyCompactNodeToolbarButtons(
            _addNodeButton,
            _editNodeButton,
            _deleteNodeButton,
            _useNodeButton,
            _testNodeButton,
            _testAllButton,
            _copyShareLinkButton,
            _exportNodesButton,
            _subscriptionsButton,
            _importTextButton,
            _importClipboardButton);

        nodeToolbarPanel.Controls.Add(_addNodeButton);
        nodeToolbarPanel.Controls.Add(_editNodeButton);
        nodeToolbarPanel.Controls.Add(_deleteNodeButton);
        nodeToolbarPanel.Controls.Add(_useNodeButton);
        nodeToolbarPanel.Controls.Add(_testNodeButton);
        nodeToolbarPanel.Controls.Add(_testAllButton);
        nodeToolbarPanel.Controls.Add(_copyShareLinkButton);
        nodeToolbarPanel.Controls.Add(_exportNodesButton);
        nodeToolbarPanel.Controls.Add(_subscriptionsButton);
        nodeToolbarPanel.Controls.Add(_importTextButton);
        nodeToolbarPanel.Controls.Add(_importClipboardButton);

        var nodeFilterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            Height = 36,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = ModernTheme.BackgroundBottom
        };
        nodeFilterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var nodeFilterControls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        _nodeSearchTextBox = new SearchBox
        {
            Width = 330,
            Height = 32,
            ForeColor = ModernTheme.Text,
            Font = ModernTheme.BodyFont,
            Margin = new Padding(0, 2, 14, 0),
            PlaceholderText = "Name, group, server or remark"
        };
        _nodeSearchTextBox.TextChanged += NodeSearchTextBoxOnTextChanged;
        nodeFilterControls.Controls.Add(_nodeSearchTextBox);

        nodeFilterControls.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 0),
            Margin = new Padding(0, 0, 6, 0),
            Text = "Group:",
            ForeColor = ModernTheme.MutedText
        });

        _groupFilterComboBox = CreateModeComboBox();
        _groupFilterComboBox.Width = 170;
        _groupFilterComboBox.Margin = new Padding(0, 1, 0, 0);
        _groupFilterComboBox.SelectedIndexChanged += GroupFilterComboBoxOnSelectedIndexChanged;
        nodeFilterControls.Controls.Add(_groupFilterComboBox);

        _nodeFilterSummaryLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(10, 8, 10, 0),
            Margin = new Padding(0, 2, 0, 0),
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = ModernTheme.MutedText,
            BackColor = ModernTheme.SurfaceMuted,
            Visible = false
        };
        ApplyRoundedRegion(_nodeFilterSummaryLabel, 12);
        _nodeFilterSummaryLabel.Resize += (_, _) => ApplyRoundedRegion(_nodeFilterSummaryLabel, 12);

        nodeFilterPanel.Controls.Add(nodeFilterControls, 0, 0);

        _nodesContextMenu = CreateNodesContextMenu();
        _nodesGrid = CreateNodesGrid();

        nodesPanel.Controls.Add(nodeToolbarPanel, 0, 0);
        nodesPanel.Controls.Add(CreateDividerLine(), 0, 1);
        nodesPanel.Controls.Add(nodeFilterPanel, 0, 3);
        nodesPanel.Controls.Add(CreateDividerLine(), 0, 5);
        nodesPanel.Controls.Add(_nodesGrid, 0, 6);

        var diagnosticsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ModernTheme.BackgroundBottom,
            Padding = new Padding(18, 8, 18, 14)
        };
        diagnosticsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        diagnosticsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var previewHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = Padding.Empty,
            BackColor = ModernTheme.BackgroundBottom
        };
        previewHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var previewLabel = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 10, 8, 0),
            Text = "Active sing-box config",
            ForeColor = ModernTheme.MutedText
        };

        _refreshButton = CreateActionButton("Refresh", () =>
        {
            RefreshView();
            return Task.CompletedTask;
        });
        _refreshButton.Margin = new Padding(8, 4, 8, 0);

        previewHeaderPanel.Controls.Add(previewLabel, 0, 0);
        previewHeaderPanel.Controls.Add(_refreshButton, 1, 0);

        _previewTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10.0f),
            BorderStyle = BorderStyle.None,
            BackColor = ModernTheme.SurfaceStrong,
            ForeColor = ModernTheme.Text,
            Margin = new Padding(8)
        };

        diagnosticsPanel.Controls.Add(previewHeaderPanel, 0, 0);
        diagnosticsPanel.Controls.Add(_previewTextBox, 0, 1);

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = ModernTheme.BackgroundBottom
        };
        contentHost.Controls.Add(diagnosticsPanel);
        contentHost.Controls.Add(nodesPanel);

        void SelectContentPage(Control selectedPage)
        {
            var showNodes = ReferenceEquals(selectedPage, nodesPanel);
            nodesPanel.Visible = showNodes;
            diagnosticsPanel.Visible = !showNodes;
            selectedPage.BringToFront();
            ApplyContentTabState(nodesTabButton, showNodes);
            ApplyContentTabState(diagnosticsTabButton, !showNodes);
        }

        nodesTabButton.Click += (_, _) => SelectContentPage(nodesPanel);
        diagnosticsTabButton.Click += (_, _) => SelectContentPage(diagnosticsPanel);
        SelectContentPage(nodesPanel);

        contentShell.Controls.Add(tabHeaderPanel, 0, 0);
        contentShell.Controls.Add(CreateDividerLine(), 0, 1);
        contentShell.Controls.Add(contentHost, 0, 2);

        Controls.Add(contentShell);
        Controls.Add(footerPanel);
        Controls.Add(commandPanel);
        Controls.Add(_statusPanel);
        RefreshView();
        Application.AddMessageFilter(this);
        _messageFilterRegistered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ClassStyle |= CsDropShadow;
            return createParams;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowChrome();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest && HandleWindowHitTest(ref message))
        {
            return;
        }

        base.WndProc(ref message);
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (IsDisposed || !Visible || !IsHandleCreated)
        {
            return false;
        }

        if (message.Msg != WmMouseMove && message.Msg != WmLeftButtonDown && message.Msg != WmLeftButtonDoubleClick)
        {
            return false;
        }

        var screenPoint = Cursor.Position;
        var clientPoint = PointToClient(screenPoint);
        if (!ClientRectangle.Contains(clientPoint))
        {
            return false;
        }

        if (WindowState == FormWindowState.Normal)
        {
            var resizeHitTest = GetResizeHitTest(clientPoint);
            if (resizeHitTest != HtClient)
            {
                if (message.Msg == WmMouseMove)
                {
                    Cursor.Current = GetResizeCursor(resizeHitTest);
                    return true;
                }

                if (message.Msg == WmLeftButtonDown)
                {
                    ReleaseCapture();
                    _ = SendMessage(Handle, WmNcLeftButtonDown, resizeHitTest, 0);
                    return true;
                }
            }
        }

        if (!IsHeaderDragPoint(clientPoint, screenPoint))
        {
            return false;
        }

        if (message.Msg == WmLeftButtonDoubleClick)
        {
            ToggleWindowMaximized();
            return true;
        }

        if (message.Msg == WmLeftButtonDown)
        {
            ReleaseCapture();
            _ = SendMessage(Handle, WmNcLeftButtonDown, HtCaption, 0);
            return true;
        }

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_messageFilterRegistered)
            {
                Application.RemoveMessageFilter(this);
                _messageFilterRegistered = false;
            }

            _controller.RuntimeStateChanged -= ControllerOnRuntimeStateChanged;
            Icon = null;
            _windowIcon.Dispose();
            _statusStoppedImage.Dispose();
            _statusWaitingImage.Dispose();
            _statusConnectedImage.Dispose();
            _statusErrorImage.Dispose();
        }

        base.Dispose(disposing);
    }

    public void RefreshView()
    {
        RefreshSummary();
        SyncSettingsControls();
        _previewTextBox.Text = _controller.GenerateConfigPreview();

        RefreshNodeFilterOptions();
        PopulateNodesGrid();
        SyncNodeActions();
    }

    public void RefreshSummary()
    {
        ApplyRuntimePresentation();
        _summaryLabel.Text = BuildHeaderSummary(_controller.RuntimeState);
        SyncConnectionButtons();
    }

    private static Button CreateActionButton(string text, Func<Task> action)
    {
        var colors = GetActionButtonColors(text);
        var button = new ActionButton
        {
            AutoSize = true,
            MinimumSize = new Size(0, 34),
            Padding = new Padding(12, 3, 12, 3),
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = colors.BackColor,
            ForeColor = colors.ForeColor,
            Font = ModernTheme.BodyFont,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Text = DecorateActionText(text)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(colors.BackColor, 0.12f);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(colors.BackColor, 0.08f);
        button.Click += async (_, _) => await action();
        return button;
    }

    private static void ApplyCompactNodeToolbarButtons(params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            button.MinimumSize = new Size(0, 28);
            button.Padding = new Padding(9, 2, 9, 2);
            button.Margin = new Padding(2, 2, 4, 2);
            button.Font = ModernTheme.SmallFont;
        }
    }

    private PictureBox CreateStatusLogoPictureBox()
    {
        return new PictureBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Width = 72,
            Height = 72,
            Margin = new Padding(0, 2, 16, 0),
            BackColor = Color.Transparent,
            Image = _statusStoppedImage,
            SizeMode = PictureBoxSizeMode.Zoom
        };
    }

    private static RoundedPanel CreateMetricCard(string title, out Label valueLabel)
    {
        var card = new RoundedPanel
        {
            Width = 134,
            Height = 46,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(10, 6, 10, 6),
            FillColor = Color.FromArgb(248, 252, 253),
            BorderColor = Color.FromArgb(218, 231, 237),
            CornerRadius = 14
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Font = ModernTheme.SmallFont,
            ForeColor = ModernTheme.MutedText,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };
        valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Font = ModernTheme.SectionFont,
            ForeColor = ModernTheme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Label CreateModeLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 7, 0, 0),
            ForeColor = ModernTheme.MutedText,
            Font = ModernTheme.BodyFont,
            Text = $"{text}:"
        };
    }

    private static ComboBox CreateModeComboBox()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = ModernTheme.SurfaceStrong,
            ForeColor = ModernTheme.Text,
            Font = ModernTheme.BodyFont,
            Width = 128
        };
    }

    private static DividerLine CreateDividerLine()
    {
        return new DividerLine
        {
            Dock = DockStyle.Fill,
            LineColor = ModernTheme.GridLine
        };
    }

    private static ContentTabButton CreateContentTabButton(string text)
    {
        var button = new ContentTabButton
        {
            Text = text
        };
        ApplyContentTabState(button, false);
        return button;
    }

    private static void ApplyContentTabState(ContentTabButton button, bool selected)
    {
        button.Selected = selected;
    }

    private void WireTitleBarDrag(Control control)
    {
        control.MouseDown += TitleBarOnMouseDown;
        control.MouseDoubleClick += TitleBarOnMouseDoubleClick;
    }

    private void TitleBarOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        _ = SendMessage(Handle, WmNcLeftButtonDown, HtCaption, 0);
    }

    private void TitleBarOnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleWindowMaximized();
        }
    }

    private void ToggleWindowMaximized()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }
        else
        {
            MaximizedBounds = Screen.FromControl(this).WorkingArea;
            WindowState = FormWindowState.Maximized;
        }

        SyncWindowCaptionButtons();
    }

    private void SyncWindowCaptionButtons()
    {
        _windowButtonStrip.Invalidate();
    }

    private void PositionWindowButtonPanel()
    {
        _windowButtonStrip.Location = new Point(Math.Max(0, _statusPanel.Width - _windowButtonStrip.Width - 1), 0);
        _windowButtonStrip.BringToFront();
        UpdateWindowCaptionBackgroundOffsets();
    }

    private void ApplyWindowCaptionColors(Color startColor, Color endColor)
    {
        _windowButtonStrip.BaseColor = endColor;
        _windowButtonStrip.BackgroundStartColor = startColor;
        _windowButtonStrip.BackgroundEndColor = endColor;
        _windowButtonStrip.BackgroundGradientMode = _statusPanel is GradientPanel gradientPanel
            ? gradientPanel.GradientMode
            : LinearGradientMode.Horizontal;
        UpdateWindowCaptionBackgroundOffsets();
    }

    private void UpdateWindowCaptionBackgroundOffsets()
    {
        _windowButtonStrip.BackgroundCanvasSize = _statusPanel.ClientSize;
        _windowButtonStrip.BackgroundOffset = GetRelativeLocation(_windowButtonStrip, _statusPanel);
        _windowButtonStrip.Invalidate();
    }

    private static Point GetRelativeLocation(Control control, Control ancestor)
    {
        var location = Point.Empty;
        for (Control? current = control; current is not null && !ReferenceEquals(current, ancestor); current = current.Parent)
        {
            location.Offset(current.Left, current.Top);
        }

        return location;
    }

    private bool IsHeaderDragPoint(Point clientPoint, Point screenPoint)
    {
        var headerDragHeight = Math.Min(38, _statusPanel.Height);
        var headerDragBounds = new Rectangle(0, 0, ClientSize.Width, headerDragHeight);
        return headerDragBounds.Contains(clientPoint) &&
            !IsPointOverWindowButton(screenPoint) &&
            !IsPointOverInteractiveHeaderControl(screenPoint);
    }

    private bool IsPointOverInteractiveHeaderControl(Point screenPoint)
    {
        return IsPointOverControl(screenPoint, _connectButton) ||
            IsPointOverControl(screenPoint, _disconnectButton) ||
            IsPointOverControl(screenPoint, _restartCoreButton);
    }

    private static bool IsPointOverControl(Point screenPoint, Control control)
    {
        return control.Visible &&
            control.Enabled &&
            control.RectangleToScreen(control.ClientRectangle).Contains(screenPoint);
    }

    private void ApplyWindowChrome()
    {
        try
        {
            var cornerPreference = DwmRoundCorners;
            _ = DwmSetWindowAttribute(
                Handle,
                DwmWindowCornerPreference,
                ref cornerPreference,
                Marshal.SizeOf<int>());
        }
        catch
        {
            // Older Windows builds do not support rounded corner preferences.
        }
    }

    private bool HandleWindowHitTest(ref Message message)
    {
        var screenPoint = new Point(GetSignedLoWord(message.LParam), GetSignedHiWord(message.LParam));
        var clientPoint = PointToClient(screenPoint);

        if (WindowState == FormWindowState.Normal)
        {
            var resizeResult = GetResizeHitTest(clientPoint);
            if (resizeResult != HtClient)
            {
                message.Result = resizeResult;
                return true;
            }
        }

        if (IsHeaderDragPoint(clientPoint, screenPoint))
        {
            message.Result = HtCaption;
            return true;
        }

        return false;
    }

    private int GetResizeHitTest(Point clientPoint)
    {
        var gripSize = Math.Max(6, (int)Math.Round(8 * DeviceDpi / 96.0));
        var onLeft = clientPoint.X <= gripSize;
        var onRight = clientPoint.X >= ClientSize.Width - gripSize;
        var onTop = clientPoint.Y <= gripSize;
        var onBottom = clientPoint.Y >= ClientSize.Height - gripSize;

        return (onLeft, onRight, onTop, onBottom) switch
        {
            (true, false, true, false) => HtTopLeft,
            (false, true, true, false) => HtTopRight,
            (true, false, false, true) => HtBottomLeft,
            (false, true, false, true) => HtBottomRight,
            (true, false, false, false) => HtLeft,
            (false, true, false, false) => HtRight,
            (false, false, true, false) => HtTop,
            (false, false, false, true) => HtBottom,
            _ => HtClient
        };
    }

    private bool IsPointOverWindowButton(Point screenPoint)
    {
        return IsPointOverControl(screenPoint, _windowButtonStrip);
    }

    private static Cursor GetResizeCursor(int hitTest)
    {
        return hitTest switch
        {
            HtLeft or HtRight => Cursors.SizeWE,
            HtTop or HtBottom => Cursors.SizeNS,
            HtTopLeft or HtBottomRight => Cursors.SizeNWSE,
            HtTopRight or HtBottomLeft => Cursors.SizeNESW,
            _ => Cursors.Default
        };
    }

    private static int GetSignedLoWord(IntPtr value)
    {
        return unchecked((short)(long)value);
    }

    private static int GetSignedHiWord(IntPtr value)
    {
        return unchecked((short)((long)value >> 16));
    }

    private static (Color BackColor, Color ForeColor) GetActionButtonColors(string text)
    {
        return text switch
        {
            "Connect" => (ModernTheme.Mint, Color.White),
            "Disconnect" => (ModernTheme.DangerSoft, ModernTheme.Danger),
            "Restart Core" => (Color.FromArgb(238, 246, 252), ModernTheme.Text),
            "Update Rules" => (ModernTheme.Accent, Color.White),
            "Self Check" => (ModernTheme.MintSoft, Color.FromArgb(28, 122, 58)),
            "Delete" => (ModernTheme.DangerSoft, ModernTheme.Danger),
            "Use as Manual" => (ModernTheme.AccentDark, Color.White),
            _ => (Color.FromArgb(238, 246, 252), ModernTheme.Text)
        };
    }

    private static string DecorateActionText(string text)
    {
        return text switch
        {
            "Connect" => "\u26A1 Connect",
            "Disconnect" => "\u2715 Disconnect",
            "Restart Core" => "\u21BB Restart Core",
            "Open Data Folder" => "\u25A3 Data Folder",
            "Open Logs" => ">_ Logs",
            "Settings" => "\u2699 Settings",
            "Update Rules" => "\u21E9 Update Rules",
            "Self Check" => "\u271A Self Check",
            "Add Node" => "+ Add Node",
            "Edit" => "\u270E Edit",
            "Delete" => "\u232B Delete",
            "Use as Manual" => "\u261D Use Manual",
            "Test Selected" => "\u25CE Test Selected",
            "Test All" => "\u25A6 Test All",
            "Copy Share Link" => "\u29C9 Copy Share",
            "Export Nodes" => "\u21E7 Export",
            "Subscriptions" => "\u25B7 Subscriptions",
            "Import Text" => "\u21E3 Import Text",
            "Import Clipboard" => "\u2398 Import Clipboard",
            "Refresh" => "\u21BB Refresh",
            _ => text
        };
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        control.Region?.Dispose();
        using var path = CreateRoundRectanglePath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }

    private static GraphicsPath CreateRoundRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }

    private DataGridView CreateNodesGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = ModernTheme.BackgroundBottom,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeight = 40,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            EnableHeadersVisualStyles = false,
            GridColor = ModernTheme.GridLine,
            MultiSelect = true,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 34 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ModernTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ModernTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ModernTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = ModernTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = ModernTheme.SectionFont;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.DefaultCellStyle.BackColor = ModernTheme.SurfaceStrong;
        grid.DefaultCellStyle.ForeColor = ModernTheme.Text;
        grid.DefaultCellStyle.SelectionBackColor = ModernTheme.AccentDark;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = ModernTheme.BodyFont;
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 252, 253);
        grid.AlternatingRowsDefaultCellStyle.ForeColor = ModernTheme.Text;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = ModernTheme.AccentDark;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Selected",
            HeaderText = "Role",
            FillWeight = 22,
            MinimumWidth = 96
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Enabled",
            HeaderText = "State",
            FillWeight = 22,
            MinimumWidth = 104
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Name",
            FillWeight = 34,
            MinimumWidth = 140
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Group",
            HeaderText = "Group",
            FillWeight = 24,
            MinimumWidth = 110
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Server",
            HeaderText = "Server",
            FillWeight = 44,
            MinimumWidth = 180
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Port",
            HeaderText = "Port",
            FillWeight = 16,
            MinimumWidth = 86
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Ping",
            HeaderText = "Ping",
            FillWeight = 24,
            MinimumWidth = 132
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "UrlTest",
            HeaderText = "URL Test",
            FillWeight = 28,
            MinimumWidth = 140
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Id",
            HeaderText = "Id",
            Visible = false
        });

        grid.SelectionChanged += (_, _) => SyncNodeActions();
        grid.CellMouseEnter += NodesGridOnCellMouseEnter;
        grid.MouseLeave += NodesGridOnMouseLeave;
        grid.CellPainting += NodesGridOnCellPainting;
        grid.MouseDown += NodesGridOnMouseDown;
        grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                await EditNodeAsync();
            }
        };

        return grid;
    }

    private ContextMenuStrip CreateNodesContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = ModernTheme.SurfaceStrong,
            ForeColor = ModernTheme.Text,
            Font = ModernTheme.BodyFont,
            RenderMode = ToolStripRenderMode.System
        };
        var addNodeItem = new ToolStripMenuItem("Add Node", null, async (_, _) => await AddNodeAsync());
        var useNowItem = new ToolStripMenuItem("Use Now (Manual Mode)", null, async (_, _) => await UseSelectedNodeAndSwitchToManualAsync());
        var useManualItem = new ToolStripMenuItem("Set as Manual Node", null, async (_, _) => await UseSelectedNodeAsync());
        var testItem = new ToolStripMenuItem("Test Selected", null, async (_, _) => await TestSelectedNodeAsync());
        var editItem = new ToolStripMenuItem("Edit", null, async (_, _) => await EditNodeAsync());
        var deleteItem = new ToolStripMenuItem("Delete", null, async (_, _) => await DeleteNodeAsync());
        var copyShareItem = new ToolStripMenuItem("Copy Share Link", null, async (_, _) => await CopySelectedNodeShareLinksAsync());
        var exportItem = new ToolStripMenuItem("Export Selected", null, async (_, _) => await ExportSelectedNodesAsync());

        menu.Items.Add(addNodeItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(useNowItem);
        menu.Items.Add(useManualItem);
        menu.Items.Add(testItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(editItem);
        menu.Items.Add(deleteItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(copyShareItem);
        menu.Items.Add(exportItem);

        menu.Opening += (_, _) =>
        {
            var selectedNodeIds = GetSelectedGridNodeIds();
            var selectedNodes = GetSelectedGridNodes();
            var firstSelectedNode = selectedNodes.FirstOrDefault();
            var hasSingleSelection = selectedNodeIds.Count == 1;
            var hasAnySelection = selectedNodeIds.Count > 0;
            var canUseFirstSelection = hasSingleSelection && firstSelectedNode?.Enabled == true;

            useNowItem.Enabled = canUseFirstSelection;
            useManualItem.Enabled = canUseFirstSelection;
            testItem.Enabled = canUseFirstSelection;
            editItem.Enabled = hasSingleSelection;
            deleteItem.Enabled = hasSingleSelection;
            copyShareItem.Enabled = hasAnySelection;
            exportItem.Enabled = hasAnySelection;
        };

        return menu;
    }

    private void PopulateNodesGrid()
    {
        var selectedNodeId = GetSelectedGridNodeId();
        _hoveredNodeRowIndex = -1;
        _nodesGrid.Rows.Clear();

        var filteredNodes = GetFilteredNodes();

        foreach (var node in filteredNodes)
        {
            var rowIndex = _nodesGrid.Rows.Add(
                BuildNodeRoleText(node),
                BuildEnabledDisplay(node.Enabled),
                node.Name,
                node.Group,
                node.Server,
                node.ServerPort,
                BuildPingDisplay(node.Id),
                BuildUrlTestDisplay(node.Id),
                node.Id);

            ApplyNodeRowStyle(_nodesGrid.Rows[rowIndex], node);
        }

        _nodeFilterSummaryLabel.Text = filteredNodes.Count == _controller.Nodes.Count
            ? $"{filteredNodes.Count} nodes"
            : $"Showing {filteredNodes.Count} of {_controller.Nodes.Count} nodes";

        if (!string.IsNullOrWhiteSpace(selectedNodeId))
        {
            SelectGridRowByNodeId(selectedNodeId);
        }

        if (_nodesGrid.SelectedRows.Count == 0 && _controller.Settings.SelectedNodeId is not null)
        {
            SelectGridRowByNodeId(_controller.Settings.SelectedNodeId);
        }
    }

    private List<NodeProfile> GetFilteredNodes()
    {
        var searchText = _nodeSearchTextBox.Text.Trim();
        var selectedGroup = _groupFilterComboBox.SelectedItem?.ToString();

        IEnumerable<NodeProfile> query = _controller.Nodes;

        if (!string.IsNullOrWhiteSpace(selectedGroup) &&
            !string.Equals(selectedGroup, AllGroupsFilterText, StringComparison.Ordinal))
        {
            query = query.Where(node => string.Equals(node.Group, selectedGroup, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(node =>
                ContainsFilterText(node.Name, searchText) ||
                ContainsFilterText(node.Group, searchText) ||
                ContainsFilterText(node.Server, searchText) ||
                ContainsFilterText(node.Remark, searchText));
        }

        return query
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RefreshNodeFilterOptions()
    {
        var previousSelection = _groupFilterComboBox.SelectedItem?.ToString() ?? AllGroupsFilterText;
        var groups = _controller.Nodes
            .Select(node => node.Group.Trim())
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _suppressGroupFilterChanged = true;
        _groupFilterComboBox.BeginUpdate();
        _groupFilterComboBox.Items.Clear();
        _groupFilterComboBox.Items.Add(AllGroupsFilterText);

        foreach (var group in groups)
        {
            _groupFilterComboBox.Items.Add(group);
        }

        var restoredSelection = _groupFilterComboBox.Items
            .Cast<object>()
            .Select(item => item.ToString())
            .FirstOrDefault(item => string.Equals(item, previousSelection, StringComparison.OrdinalIgnoreCase))
            ?? AllGroupsFilterText;

        _groupFilterComboBox.SelectedItem = restoredSelection;
        _groupFilterComboBox.EndUpdate();
        _suppressGroupFilterChanged = false;
    }

    private static bool ContainsFilterText(string? source, string searchText)
    {
        return source?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    private string BuildNodeRoleText(NodeProfile node)
    {
        if (string.Equals(node.Id, _controller.Settings.SelectedNodeId, StringComparison.Ordinal))
        {
            return "Manual";
        }

        if (string.Equals(node.Id, _controller.RuntimeState.CurrentRealNodeId, StringComparison.Ordinal))
        {
            return "Active";
        }

        return node.SubscriptionId.Length > 0 ? "Subscription" : "Local";
    }

    private static string BuildEnabledDisplay(bool enabled)
    {
        return enabled ? "\u25CF Enabled" : "\u25CF Disabled";
    }

    private string BuildPingDisplay(string nodeId)
    {
        return _controller.GetNodeLatencyDisplay(nodeId);
    }

    private string BuildUrlTestDisplay(string nodeId)
    {
        return _controller.GetNodeUrlTestLatencyDisplay(nodeId);
    }

    private void ApplyNodeRowStyle(DataGridViewRow row, NodeProfile node)
    {
        row.DefaultCellStyle.BackColor = ModernTheme.SurfaceStrong;
        row.DefaultCellStyle.ForeColor = ModernTheme.Text;
        row.DefaultCellStyle.SelectionBackColor = ModernTheme.AccentDark;
        row.DefaultCellStyle.SelectionForeColor = Color.White;

        if (!node.Enabled)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(244, 247, 249);
            row.DefaultCellStyle.ForeColor = ModernTheme.Neutral;
        }

        if (row.Index == _hoveredNodeRowIndex && !row.Selected)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(237, 247, 251);
        }

        if (string.Equals(node.Id, _controller.Settings.SelectedNodeId, StringComparison.Ordinal))
        {
            row.DefaultCellStyle.BackColor = ModernTheme.MintSoft;
            row.Cells["Selected"].Style.ForeColor = Color.FromArgb(28, 122, 58);
            row.Cells["Selected"].Style.Font = ModernTheme.SectionFont;
        }
        else if (string.Equals(node.Id, _controller.RuntimeState.CurrentRealNodeId, StringComparison.Ordinal))
        {
            row.Cells["Selected"].Style.ForeColor = ModernTheme.Accent;
            row.Cells["Selected"].Style.Font = ModernTheme.SectionFont;
        }

        row.Cells["Enabled"].Style.ForeColor = node.Enabled
            ? ModernTheme.Mint
            : ModernTheme.Danger;

        ApplyLatencyCellStyle(row, "Ping");
        ApplyLatencyCellStyle(row, "UrlTest");
    }

    private static void ApplyLatencyCellStyle(DataGridViewRow row, string columnName)
    {
        var delayText = row.Cells[columnName].Value?.ToString();
        row.Cells[columnName].Style.ForeColor = TryParseLatencyDisplay(delayText, out var latency)
            ? GetLatencyColor(latency)
            : ModernTheme.Neutral;
        row.Cells[columnName].Style.Font = ModernTheme.SectionFont;
    }

    private static Color GetLatencyColor(int latencyMilliseconds)
    {
        if (latencyMilliseconds < 100)
        {
            return ModernTheme.Mint;
        }

        if (latencyMilliseconds <= 300)
        {
            return ModernTheme.Warning;
        }

        return ModernTheme.Danger;
    }

    private static bool TryParseLatencyDisplay(string? display, out int latencyMilliseconds)
    {
        latencyMilliseconds = 0;
        if (string.IsNullOrWhiteSpace(display))
        {
            return false;
        }

        var normalized = display.Replace("\u25CF", string.Empty, StringComparison.Ordinal).Trim();
        if (!normalized.EndsWith(" ms", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numberText = normalized[..^3].Trim();
        return int.TryParse(numberText, out latencyMilliseconds);
    }

    private void NodesGridOnCellMouseEnter(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            SetHoveredNodeRow(e.RowIndex);
        }
    }

    private void NodesGridOnMouseLeave(object? sender, EventArgs e)
    {
        SetHoveredNodeRow(-1);
    }

    private void SetHoveredNodeRow(int rowIndex)
    {
        if (_hoveredNodeRowIndex == rowIndex)
        {
            return;
        }

        var previousRowIndex = _hoveredNodeRowIndex;
        _hoveredNodeRowIndex = rowIndex;
        RestyleNodeGridRow(previousRowIndex);
        RestyleNodeGridRow(_hoveredNodeRowIndex);
    }

    private void RestyleNodeGridRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _nodesGrid.Rows.Count)
        {
            return;
        }

        var row = _nodesGrid.Rows[rowIndex];
        var nodeId = row.Cells["Id"].Value?.ToString();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        var node = _controller.Nodes.FirstOrDefault(current => string.Equals(current.Id, nodeId, StringComparison.Ordinal));
        if (node is not null)
        {
            ApplyNodeRowStyle(row, node);
        }
    }

    private void NodesGridOnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            !IsLatencyColumn(_nodesGrid.Columns[e.ColumnIndex].Name))
        {
            return;
        }

        e.Handled = true;
        e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

        var graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        var cellStyle = e.CellStyle ?? _nodesGrid.DefaultCellStyle;
        var display = e.FormattedValue?.ToString() ?? string.Empty;
        var hasLatency = TryParseLatencyDisplay(display, out var latency);
        var selected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
        var color = hasLatency ? GetLatencyColor(latency) : ModernTheme.Neutral;
        var textColor = selected ? cellStyle.SelectionForeColor : color;
        var contentBounds = new Rectangle(
            e.CellBounds.Left,
            e.CellBounds.Top,
            e.CellBounds.Width,
            Math.Max(1, e.CellBounds.Height - 1));

        if (selected)
        {
            var selectionBackground = cellStyle.SelectionBackColor.IsEmpty
                ? _nodesGrid.DefaultCellStyle.SelectionBackColor
                : cellStyle.SelectionBackColor;
            using var selectionBrush = new SolidBrush(selectionBackground);
            graphics.FillRectangle(selectionBrush, contentBounds);
        }

        DrawLatencyDot(graphics, contentBounds, selected ? Color.FromArgb(224, 238, 255) : color);

        var textBounds = new Rectangle(contentBounds.Left + 24, contentBounds.Top, Math.Max(10, contentBounds.Width - 78), contentBounds.Height);
        TextRenderer.DrawText(
            graphics,
            display,
            cellStyle.Font ?? ModernTheme.BodyFont,
            textBounds,
            textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if (hasLatency)
        {
            DrawLatencyBars(graphics, contentBounds, latency, selected);
        }
    }

    private static bool IsLatencyColumn(string columnName)
    {
        return string.Equals(columnName, "Ping", StringComparison.Ordinal) ||
            string.Equals(columnName, "UrlTest", StringComparison.Ordinal);
    }

    private static void DrawLatencyBars(Graphics graphics, Rectangle cellBounds, int latencyMilliseconds, bool selected)
    {
        var activeBars = latencyMilliseconds switch
        {
            < 100 => 5,
            <= 180 => 4,
            <= 300 => 3,
            <= 600 => 2,
            _ => 1
        };
        var barColor = selected ? Color.FromArgb(224, 238, 255) : GetLatencyColor(latencyMilliseconds);
        var inactiveColor = selected ? Color.FromArgb(116, 143, 170) : Color.FromArgb(220, 230, 235);
        var left = cellBounds.Right - 46;
        var bottom = cellBounds.Top + (cellBounds.Height + 16) / 2;

        for (var index = 0; index < 5; index++)
        {
            var height = 4 + (index * 3);
            var rect = new Rectangle(left + index * 7, bottom - height, 4, height);
            using var brush = new SolidBrush(index < activeBars ? barColor : inactiveColor);
            graphics.FillRectangle(brush, rect);
        }
    }

    private static void DrawLatencyDot(Graphics graphics, Rectangle cellBounds, Color color)
    {
        var dotSize = 7;
        var dotBounds = new Rectangle(
            cellBounds.Left + 11,
            cellBounds.Top + (cellBounds.Height - dotSize) / 2,
            dotSize,
            dotSize);

        var previousMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, dotBounds);
        graphics.SmoothingMode = previousMode;
    }

    private async Task AddNodeAsync()
    {
        using var dialog = new NodeEditorForm(_controller.CreateNodeDraft());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.AddNodeAsync(dialog.NodeProfile, ct));
    }

    private async Task EditNodeAsync()
    {
        var nodeId = GetSelectedGridNodeId();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        using var dialog = new NodeEditorForm(_controller.CreateNodeDraft(nodeId));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.UpdateNodeAsync(dialog.NodeProfile, ct));
    }

    private async Task DeleteNodeAsync()
    {
        var nodeId = GetSelectedGridNodeId();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        var node = _controller.Nodes.FirstOrDefault(current => current.Id == nodeId);
        var message = node is null
            ? "Delete this node?"
            : $"Delete node \"{node.Name}\"?";

        if (MessageBox.Show(this, message, "EasyNaive", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.RemoveNodeAsync(nodeId, ct));
    }

    private async Task UseSelectedNodeAsync()
    {
        var nodeId = GetSelectedGridNodeId();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        await ExecuteAsync(ct => _controller.SetSelectedNodeAsync(nodeId, ct));
    }

    private async Task UseSelectedNodeAndSwitchToManualAsync()
    {
        var nodeId = GetSelectedGridNodeId();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            await _controller.SetSelectedNodeAsync(nodeId, ct);
            await _controller.SetNodeModeAsync(NodeMode.Manual, ct);
        });
    }

    private async Task TestSelectedNodeAsync()
    {
        var nodeId = GetSelectedGridNodeId();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        await ExecuteAsync(async ct => _ = await _controller.TestNodeLatencyAsync(nodeId, ct));
    }

    private async Task TestAllNodesAsync()
    {
        await ExecuteAsync(_controller.TestAllNodeLatenciesAsync);
    }

    private Task CopySelectedNodeShareLinksAsync()
    {
        try
        {
            var selectedNodes = GetSelectedGridNodes();
            if (selectedNodes.Count == 0)
            {
                MessageBox.Show(this, "Select at least one node to share.", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            Clipboard.SetText(NodeShareFormatter.FormatNodes(selectedNodes));
            MessageBox.Show(this, $"Copied {selectedNodes.Count} node share link(s).", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }

        return Task.CompletedTask;
    }

    private Task ExportSelectedNodesAsync()
    {
        try
        {
            var selectedNodes = GetSelectedGridNodes();
            if (selectedNodes.Count == 0)
            {
                MessageBox.Show(this, "Select at least one node to export.", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "txt",
                FileName = $"EasyNaive-nodes-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                Filter = "Naive node list (*.txt)|*.txt|All files (*.*)|*.*",
                OverwritePrompt = true,
                Title = "Export EasyNaive Nodes"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return Task.CompletedTask;
            }

            File.WriteAllText(dialog.FileName, NodeShareFormatter.FormatNodes(selectedNodes));
            MessageBox.Show(this, $"Exported {selectedNodes.Count} node(s).", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }

        return Task.CompletedTask;
    }

    private Task OpenSubscriptionsAsync()
    {
        using var dialog = new SubscriptionManagerForm(_controller);
        dialog.ShowDialog(this);
        RefreshView();
        return Task.CompletedTask;
    }

    private Task OpenLogsAsync()
    {
        using var dialog = new LogViewerForm(_controller);
        dialog.ShowDialog(this);
        return Task.CompletedTask;
    }

    private async Task RunSelfCheckAsync()
    {
        ToggleActions(false);

        try
        {
            var report = await _controller.RunSelfCheckAsync(CancellationToken.None);
            MessageBox.Show(
                this,
                report.ToDisplayText(),
                report.HasFailures ? "EasyNaive Self Check" : "EasyNaive Self Check Passed",
                MessageBoxButtons.OK,
                report.HasFailures ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            ToggleActions(true);
            RefreshView();
        }
    }

    private async Task OpenSettingsAsync()
    {
        using var dialog = new SettingsForm(_controller.CreateSettingsDraft());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.UpdateGeneralSettingsAsync(dialog.Settings, ct));
    }

    private async Task UpdateRulesAsync()
    {
        ToggleActions(false);

        try
        {
            var summary = await _controller.UpdateRuleSetsAsync(CancellationToken.None);
            MessageBox.Show(this, summary, "EasyNaive Rule-Sets", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            ToggleActions(true);
            RefreshView();
        }
    }

    private Task OpenTextImportAsync()
    {
        return OpenImportDialogAsync("Manual Import", string.Empty);
    }

    private Task OpenClipboardImportAsync()
    {
        string clipboardText;

        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show(this, "Clipboard does not contain text.", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            clipboardText = Clipboard.GetText();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Task.CompletedTask;
        }

        return OpenImportDialogAsync("Clipboard Import", clipboardText);
    }

    private async Task OpenImportDialogAsync(string sourceName, string content)
    {
        using var dialog = new ManualImportForm(sourceName, content);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ManualImportPreview preview;
        try
        {
            preview = _controller.PreviewNodesFromText(dialog.SourceName, dialog.ImportContent);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }

        using var previewDialog = new ManualImportPreviewForm(preview);
        if (previewDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(async ct => _ = await _controller.ImportNodesFromPreviewAsync(preview, previewDialog.SkipDuplicates, ct));
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        ToggleActions(false);

        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            ToggleActions(true);
            RefreshView();
        }
    }

    private void ShowError(Exception exception)
    {
        MessageBox.Show(
            this,
            ErrorMessageTranslator.ToDisplayMessage(exception),
            "EasyNaive",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void RefreshNodeList()
    {
        PopulateNodesGrid();
        SyncNodeActions();
    }

    private string? GetSelectedGridNodeId()
    {
        return GetSelectedGridNodeIds().FirstOrDefault();
    }

    private List<string> GetSelectedGridNodeIds()
    {
        return _nodesGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Select(row => row.Cells["Id"].Value?.ToString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => id!)
            .ToList();
    }

    private List<NodeProfile> GetSelectedGridNodes()
    {
        var selectedNodeIds = GetSelectedGridNodeIds();
        return _controller.Nodes
            .Where(node => selectedNodeIds.Contains(node.Id, StringComparer.Ordinal))
            .OrderBy(node => selectedNodeIds.IndexOf(node.Id))
            .ToList();
    }

    private void SelectGridRowByNodeId(string nodeId)
    {
        foreach (DataGridViewRow row in _nodesGrid.Rows)
        {
            var currentId = row.Cells["Id"].Value?.ToString();
            row.Selected = string.Equals(currentId, nodeId, StringComparison.Ordinal);
        }
    }

    private void NodesGridOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hitTest = _nodesGrid.HitTest(e.X, e.Y);
        if (hitTest.RowIndex < 0)
        {
            _nodesGrid.ClearSelection();
            _nodesContextMenu.Show(_nodesGrid, e.Location);
            return;
        }

        var row = _nodesGrid.Rows[hitTest.RowIndex];
        if (!row.Selected)
        {
            _nodesGrid.ClearSelection();
            row.Selected = true;
        }

        _nodesGrid.CurrentCell = row.Cells[Math.Max(hitTest.ColumnIndex, 0)];
        _nodesContextMenu.Show(_nodesGrid, e.Location);
    }

    private void SyncNodeActions()
    {
        var selectedNodeId = GetSelectedGridNodeId();
        var hasSelection = !string.IsNullOrWhiteSpace(selectedNodeId);
        var selectedNode = hasSelection
            ? _controller.Nodes.FirstOrDefault(node => node.Id == selectedNodeId)
            : null;
        var canUseSelection = selectedNode?.Enabled == true;

        _editNodeButton.Enabled = hasSelection;
        _deleteNodeButton.Enabled = hasSelection;
        _useNodeButton.Enabled = canUseSelection;
        _testNodeButton.Enabled = canUseSelection;
        _copyShareLinkButton.Enabled = hasSelection;
        _exportNodesButton.Enabled = hasSelection;
    }

    private void ToggleActions(bool enabled)
    {
        _connectButton.Enabled = enabled;
        _disconnectButton.Enabled = enabled;
        _restartCoreButton.Enabled = enabled;
        _refreshButton.Enabled = enabled;
        _openDataButton.Enabled = enabled;
        _openLogsButton.Enabled = enabled;
        _settingsButton.Enabled = enabled;
        _updateRulesButton.Enabled = enabled;
        _selfCheckButton.Enabled = enabled;
        _subscriptionsButton.Enabled = enabled;
        _importTextButton.Enabled = enabled;
        _importClipboardButton.Enabled = enabled;
        _autoStartCheckBox.Enabled = enabled;
        _captureModeSwitchButton.Enabled = enabled;
        _routeModeComboBox.Enabled = enabled;
        _nodeModeComboBox.Enabled = enabled;
        _addNodeButton.Enabled = enabled;
        _copyShareLinkButton.Enabled = enabled;
        _exportNodesButton.Enabled = enabled;
        _testAllButton.Enabled = enabled;

        SyncNodeActions();

        if (!enabled)
        {
            _editNodeButton.Enabled = false;
            _deleteNodeButton.Enabled = false;
            _useNodeButton.Enabled = false;
            _testNodeButton.Enabled = false;
            _testAllButton.Enabled = false;
            _copyShareLinkButton.Enabled = false;
            _exportNodesButton.Enabled = false;
            return;
        }

        SyncConnectionButtons();
    }

    private void ControllerOnRuntimeStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new MethodInvoker(RefreshSummary));
            return;
        }

        RefreshSummary();
    }

    private void ApplyRuntimePresentation()
    {
        var runtimeState = _controller.RuntimeState;
        var selectedNodeName = _controller.SelectedNode?.Name ?? "No node selected";

        Color startColor;
        Color endColor;
        Color foreColor;
        string statusText;

        switch (runtimeState.CoreStatus)
        {
            case CoreStatus.Running:
                startColor = Color.FromArgb(219, 248, 225);
                endColor = Color.FromArgb(235, 250, 247);
                foreColor = Color.FromArgb(18, 115, 54);
                statusText = "Connected";
                break;
            case CoreStatus.Starting:
                startColor = ModernTheme.WarningSoft;
                endColor = Color.FromArgb(249, 246, 235);
                foreColor = Color.FromArgb(143, 92, 9);
                statusText = "Connecting";
                break;
            case CoreStatus.Stopping:
                startColor = ModernTheme.WarningSoft;
                endColor = Color.FromArgb(249, 246, 235);
                foreColor = Color.FromArgb(143, 92, 9);
                statusText = "Disconnecting";
                break;
            case CoreStatus.Error:
                startColor = ModernTheme.DangerSoft;
                endColor = Color.FromArgb(250, 237, 242);
                foreColor = ModernTheme.Danger;
                statusText = "Connection Error";
                break;
            default:
                startColor = ModernTheme.SurfaceMuted;
                endColor = ModernTheme.BackgroundBottom;
                foreColor = ModernTheme.Neutral;
                statusText = "Disconnected";
                break;
        }

        if (_statusPanel is GradientPanel gradientPanel)
        {
            gradientPanel.StartColor = startColor;
            gradientPanel.EndColor = endColor;
            gradientPanel.Invalidate(true);
        }
        else
        {
            _statusPanel.BackColor = startColor;
        }

        ApplyWindowCaptionColors(startColor, endColor);
        InvalidateHeaderActionButtons();
        _summaryLabel.ForeColor = foreColor;
        _statusLabel.ForeColor = foreColor;
        _statusDetailLabel.ForeColor = ModernTheme.Text;
        _trafficLabel.ForeColor = foreColor;
        _statusLabel.Text = statusText;
        _statusDetailLabel.Text = BuildStatusDetail(runtimeState, selectedNodeName);
        _trafficLabel.Text = BuildTrafficDetail(runtimeState);
        ApplyStatusDashboard(runtimeState, statusText, foreColor);
        Text = $"EasyNaive - {statusText}";
    }

    private void InvalidateHeaderActionButtons()
    {
        _connectButton.Invalidate();
        _disconnectButton.Invalidate();
        _restartCoreButton.Invalidate();
        _windowButtonStrip.Invalidate();
    }

    private void ApplyStatusDashboard(RuntimeState runtimeState, string statusText, Color statusColor)
    {
        var isRunning = runtimeState.CoreStatus == CoreStatus.Running;
        var isBusy = runtimeState.CoreStatus == CoreStatus.Starting || runtimeState.CoreStatus == CoreStatus.Stopping;

        _statusLogoPictureBox.Image = runtimeState.CoreStatus switch
        {
            CoreStatus.Running => _statusConnectedImage,
            CoreStatus.Starting => _statusWaitingImage,
            CoreStatus.Stopping => _statusWaitingImage,
            CoreStatus.Error => _statusErrorImage,
            _ => _statusStoppedImage
        };
        _statusLogoPictureBox.BackColor = Color.Transparent;

        _healthMetricValueLabel.Text = statusText;
        _healthMetricValueLabel.ForeColor = statusColor;
        _nodesMetricValueLabel.Text = $"{_controller.Nodes.Count} / {_controller.Subscriptions.Count} subs";
        _nodesMetricValueLabel.ForeColor = ModernTheme.Text;
        _pidMetricValueLabel.Text = runtimeState.ProcessId is int processId ? processId.ToString() : "-";
        _pidMetricValueLabel.ForeColor = runtimeState.ProcessId is null ? ModernTheme.Neutral : ModernTheme.Text;
        _modeMetricValueLabel.Text = $"{_controller.Settings.CaptureMode} / {_controller.Settings.RouteMode}";
        _modeMetricValueLabel.ForeColor = ModernTheme.Text;
    }

    private string BuildHeaderSummary(RuntimeState runtimeState)
    {
        var parts = new List<string>
        {
            $"Node: {BuildDisplayedNodeName(runtimeState, _controller.SelectedNode?.Name ?? "None")}",
            $"{_controller.Settings.CaptureMode} / {_controller.Settings.RouteMode} / {_controller.Settings.NodeMode}",
            $"{_controller.Nodes.Count} nodes"
        };

        if (_controller.Subscriptions.Count > 0)
        {
            parts.Add($"{_controller.Subscriptions.Count} subs");
        }

        if (runtimeState.CurrentLatency is int latency)
        {
            parts.Add($"Ping {latency} ms");
        }

        if (runtimeState.ProcessId is int processId)
        {
            parts.Add($"PID {processId}");
        }

        if (!string.IsNullOrWhiteSpace(runtimeState.LastError))
        {
            parts.Add($"Error: {ErrorMessageTranslator.ToDisplayMessage(runtimeState.LastError)}");
        }

        return string.Join("  |  ", parts);
    }

    private string BuildStatusDetail(RuntimeState runtimeState, string selectedNodeName)
    {
        if (runtimeState.CoreStatus == CoreStatus.Running)
        {
            var helperDetail = _controller.Settings.CaptureMode == CaptureMode.Tun && !string.IsNullOrWhiteSpace(runtimeState.ElevationStatusDetail)
                ? $" | Helper: {runtimeState.ElevationStatusDetail}"
                : string.Empty;
            return $"Node: {BuildDisplayedNodeName(runtimeState, selectedNodeName)} | Capture: {_controller.Settings.CaptureMode} | Route: {_controller.Settings.RouteMode} | Mode: {_controller.Settings.NodeMode}{helperDetail}";
        }

        if (runtimeState.CoreStatus == CoreStatus.Error && !string.IsNullOrWhiteSpace(runtimeState.LastError))
        {
            return ErrorMessageTranslator.ToDisplayMessage(runtimeState.LastError);
        }

        return string.IsNullOrWhiteSpace(runtimeState.StatusDetail)
            ? $"Node: {selectedNodeName}"
            : runtimeState.StatusDetail;
    }

    private string BuildDisplayedNodeName(RuntimeState runtimeState, string selectedNodeName)
    {
        var currentRealNode = _controller.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, runtimeState.CurrentRealNodeId, StringComparison.Ordinal));

        if (currentRealNode is not null)
        {
            return _controller.Settings.NodeMode == NodeMode.Auto
                ? $"Auto -> {currentRealNode.Name}"
                : currentRealNode.Name;
        }

        return _controller.Settings.NodeMode == NodeMode.Auto
            ? $"Auto -> {selectedNodeName}"
            : selectedNodeName;
    }

    private static string BuildTrafficDetail(RuntimeState runtimeState)
    {
        return $"Down: {TrafficFormatter.FormatRate(runtimeState.DownloadRateBytesPerSecond)} | Up: {TrafficFormatter.FormatRate(runtimeState.UploadRateBytesPerSecond)} | Total Down: {TrafficFormatter.FormatBytes(runtimeState.DownloadTotalBytes)} | Total Up: {TrafficFormatter.FormatBytes(runtimeState.UploadTotalBytes)} | Connections: {runtimeState.ActiveConnections}";
    }

    private void SyncSettingsControls()
    {
        _suppressModeSelectionChanged = true;
        _captureModeSwitchButton.Checked = _controller.Settings.CaptureMode == CaptureMode.Tun;
        _routeModeComboBox.SelectedItem = _controller.Settings.RouteMode;
        _nodeModeComboBox.SelectedItem = _controller.Settings.NodeMode;
        _suppressModeSelectionChanged = false;

        _suppressAutoStartChanged = true;
        _autoStartCheckBox.Checked = _controller.Settings.EnableAutoStart;
        _suppressAutoStartChanged = false;
    }

    private void SyncConnectionButtons()
    {
        var status = _controller.RuntimeState.CoreStatus;
        var isBusy = status == CoreStatus.Starting || status == CoreStatus.Stopping;

        _connectButton.Enabled = !isBusy && status != CoreStatus.Running;
        _disconnectButton.Enabled = !isBusy && status != CoreStatus.Stopped;
        _restartCoreButton.Enabled = !isBusy;
    }

    private void NodeSearchTextBoxOnTextChanged(object? sender, EventArgs e)
    {
        RefreshNodeList();
    }

    private void GroupFilterComboBoxOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressGroupFilterChanged)
        {
            return;
        }

        RefreshNodeList();
    }

    private async void AutoStartCheckBoxOnCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressAutoStartChanged)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.SetAutoStartAsync(_autoStartCheckBox.Checked, ct));
    }

    private async void CaptureModeSwitchButtonOnCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressModeSelectionChanged)
        {
            return;
        }

        var captureMode = _captureModeSwitchButton.Checked ? CaptureMode.Tun : CaptureMode.Proxy;
        _captureModeSwitchButton.Enabled = false;

        try
        {
            await _controller.SetCaptureModeAsync(captureMode, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _captureModeSwitchButton.Enabled = true;
            RefreshView();
        }
    }

    private async void RouteModeComboBoxOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressModeSelectionChanged || _routeModeComboBox.SelectedItem is not RouteMode routeMode)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.SetRouteModeAsync(routeMode, ct));
    }

    private async void NodeModeComboBoxOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressModeSelectionChanged || _nodeModeComboBox.SelectedItem is not NodeMode nodeMode)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.SetNodeModeAsync(nodeMode, ct));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, int wParam, int lParam);
}
