using System.Drawing;
using System.Windows.Forms;
using EasyNaive.App.Presentation;
using EasyNaive.App.Sharing;
using EasyNaive.Core.Enums;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Forms;

internal sealed class MainForm : Form
{
    private const string AllGroupsFilterText = "All groups";

    private readonly CoreController _controller;
    private readonly Panel _statusPanel;
    private readonly Label _statusLabel;
    private readonly Label _statusDetailLabel;
    private readonly Label _trafficLabel;
    private readonly Label _summaryLabel;
    private readonly ComboBox _captureModeComboBox;
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
    private readonly TextBox _nodeSearchTextBox;
    private readonly ComboBox _groupFilterComboBox;
    private readonly Label _nodeFilterSummaryLabel;
    private readonly TextBox _previewTextBox;
    private readonly CheckBox _autoStartCheckBox;
    private readonly Icon _windowIcon;
    private bool _suppressAutoStartChanged;
    private bool _suppressGroupFilterChanged;
    private bool _suppressModeSelectionChanged;

    public MainForm(CoreController controller)
    {
        _controller = controller;
        _controller.RuntimeStateChanged += ControllerOnRuntimeStateChanged;

        Text = "EasyNaive";
        _windowIcon = AppIcons.CreateApplicationIcon();
        Icon = _windowIcon;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 680);

        _statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 128,
            Padding = new Padding(16, 14, 16, 14)
        };

        _summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18.0f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusDetailLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _trafficLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _connectButton = CreateActionButton("Connect", async () => await ExecuteAsync(_controller.ConnectAsync));
        _disconnectButton = CreateActionButton("Disconnect", async () => await ExecuteAsync(_controller.DisconnectAsync));
        _restartCoreButton = CreateActionButton("Restart Core", async () => await ExecuteAsync(_controller.RestartCoreAsync));
        _openDataButton = CreateActionButton("Open Data Folder", () =>
        {
            _controller.OpenDataDirectory();
            return Task.CompletedTask;
        });
        _openLogsButton = CreateActionButton("Open Logs", () =>
        {
            _controller.OpenLogsDirectory();
            return Task.CompletedTask;
        });
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
            Margin = Padding.Empty
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var statusTextPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty
        };
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        statusTextPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statusTextPanel.Controls.Add(_summaryLabel, 0, 0);
        statusTextPanel.Controls.Add(_statusLabel, 0, 1);
        statusTextPanel.Controls.Add(_statusDetailLabel, 0, 2);
        statusTextPanel.Controls.Add(_trafficLabel, 0, 3);

        var primaryActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = Padding.Empty,
            WrapContents = false
        };
        primaryActionPanel.Controls.Add(_connectButton);
        primaryActionPanel.Controls.Add(_disconnectButton);
        primaryActionPanel.Controls.Add(_restartCoreButton);

        var utilityActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, 10, 0, 0),
            WrapContents = false
        };
        utilityActionPanel.Controls.Add(_openDataButton);
        utilityActionPanel.Controls.Add(_openLogsButton);
        utilityActionPanel.Controls.Add(_settingsButton);
        utilityActionPanel.Controls.Add(_updateRulesButton);
        utilityActionPanel.Controls.Add(_selfCheckButton);

        var statusActionHost = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(24, 4, 0, 0)
        };
        statusActionHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusActionHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusActionHost.Controls.Add(primaryActionPanel, 0, 0);
        statusActionHost.Controls.Add(utilityActionPanel, 0, 1);

        statusLayout.Controls.Add(statusTextPanel, 0, 0);
        statusLayout.Controls.Add(statusActionHost, 1, 0);
        _statusPanel.Controls.Add(statusLayout);

        var commandPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(12, 8, 12, 0)
        };
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var modePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            WrapContents = false
        };
        modePanel.Controls.Add(CreateModeLabel("Capture"));
        _captureModeComboBox = CreateModeComboBox();
        _captureModeComboBox.Items.AddRange([CaptureMode.Proxy, CaptureMode.Tun]);
        _captureModeComboBox.SelectedIndexChanged += CaptureModeComboBoxOnSelectedIndexChanged;
        modePanel.Controls.Add(_captureModeComboBox);

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
            Padding = new Padding(0, 6, 0, 0)
        };
        _autoStartCheckBox.CheckedChanged += AutoStartCheckBoxOnCheckedChanged;

        var utilityPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            WrapContents = false,
            Margin = Padding.Empty
        };
        utilityPanel.Controls.Add(_autoStartCheckBox);

        commandPanel.Controls.Add(modePanel, 0, 0);
        commandPanel.Controls.Add(utilityPanel, 1, 0);

        var nodesPage = new TabPage("Nodes")
        {
            Padding = new Padding(0),
            UseVisualStyleBackColor = true
        };

        var diagnosticsPage = new TabPage("Diagnostics")
        {
            Padding = new Padding(0),
            UseVisualStyleBackColor = true
        };

        var contentTabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var nodesPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        nodesPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var nodeHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        nodeHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        nodeHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var nodeActionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            Padding = new Padding(8, 8, 8, 8),
            WrapContents = true
        };

        _addNodeButton = CreateActionButton("Add Node", AddNodeAsync);
        _editNodeButton = CreateActionButton("Edit", EditNodeAsync);
        _deleteNodeButton = CreateActionButton("Delete", DeleteNodeAsync);
        _useNodeButton = CreateActionButton("Use as Manual", UseSelectedNodeAsync);
        _testNodeButton = CreateActionButton("Test Selected", TestSelectedNodeAsync);
        _testAllButton = CreateActionButton("Test All", TestAllNodesAsync);
        _copyShareLinkButton = CreateActionButton("Copy Share Link", CopySelectedNodeShareLinksAsync);
        _exportNodesButton = CreateActionButton("Export Nodes", ExportSelectedNodesAsync);

        nodeActionPanel.Controls.Add(_addNodeButton);
        nodeActionPanel.Controls.Add(_editNodeButton);
        nodeActionPanel.Controls.Add(_deleteNodeButton);
        nodeActionPanel.Controls.Add(_useNodeButton);
        nodeActionPanel.Controls.Add(_testNodeButton);
        nodeActionPanel.Controls.Add(_testAllButton);
        nodeActionPanel.Controls.Add(_copyShareLinkButton);
        nodeActionPanel.Controls.Add(_exportNodesButton);
        nodeActionPanel.Controls.Add(_subscriptionsButton);
        nodeActionPanel.Controls.Add(_importTextButton);
        nodeActionPanel.Controls.Add(_importClipboardButton);

        var nodeFilterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8, 0, 8, 2),
            Margin = Padding.Empty
        };
        nodeFilterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nodeFilterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var nodeFilterControls = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = Padding.Empty
        };

        nodeFilterControls.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            Text = "Search:"
        });

        _nodeSearchTextBox = new TextBox
        {
            Width = 240,
            PlaceholderText = "Name, group, server or remark"
        };
        _nodeSearchTextBox.TextChanged += NodeSearchTextBoxOnTextChanged;
        nodeFilterControls.Controls.Add(_nodeSearchTextBox);

        nodeFilterControls.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(8, 8, 0, 0),
            Text = "Group:"
        });

        _groupFilterComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _groupFilterComboBox.SelectedIndexChanged += GroupFilterComboBoxOnSelectedIndexChanged;
        nodeFilterControls.Controls.Add(_groupFilterComboBox);

        _nodeFilterSummaryLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(0, 8, 0, 0),
            TextAlign = ContentAlignment.MiddleRight
        };

        nodeFilterPanel.Controls.Add(nodeFilterControls, 0, 0);
        nodeFilterPanel.Controls.Add(_nodeFilterSummaryLabel, 1, 0);

        _nodesContextMenu = CreateNodesContextMenu();
        _nodesGrid = CreateNodesGrid();

        nodeHeaderPanel.Controls.Add(nodeActionPanel, 0, 0);
        nodeHeaderPanel.Controls.Add(nodeFilterPanel, 0, 1);

        nodesPanel.Controls.Add(nodeHeaderPanel, 0, 0);
        nodesPanel.Controls.Add(_nodesGrid, 0, 1);

        var diagnosticsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        diagnosticsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        diagnosticsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var previewHeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = Padding.Empty
        };
        previewHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var previewLabel = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 10, 8, 0),
            Text = "Active sing-box config"
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
            Margin = new Padding(8)
        };

        diagnosticsPanel.Controls.Add(previewHeaderPanel, 0, 0);
        diagnosticsPanel.Controls.Add(_previewTextBox, 0, 1);

        nodesPage.Controls.Add(nodesPanel);
        diagnosticsPage.Controls.Add(diagnosticsPanel);
        contentTabs.TabPages.Add(nodesPage);
        contentTabs.TabPages.Add(diagnosticsPage);

        Controls.Add(contentTabs);
        Controls.Add(commandPanel);
        Controls.Add(_statusPanel);

        RefreshView();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.RuntimeStateChanged -= ControllerOnRuntimeStateChanged;
            Icon = null;
            _windowIcon.Dispose();
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
        var button = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(0, 30),
            Padding = new Padding(8, 2, 8, 2),
            Text = text
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static Label CreateModeLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 7, 0, 0),
            Text = $"{text}:"
        };
    }

    private static ComboBox CreateModeComboBox()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 116
        };
    }

    private DataGridView CreateNodesGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            ColumnHeadersHeight = 34,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            MultiSelect = true,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Selected",
            HeaderText = "Role",
            FillWeight = 18
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Enabled",
            HeaderText = "State",
            FillWeight = 18
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Name",
            FillWeight = 36
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Group",
            HeaderText = "Group",
            FillWeight = 28
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Server",
            HeaderText = "Server",
            FillWeight = 42
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Port",
            HeaderText = "Port",
            FillWeight = 18
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Delay",
            HeaderText = "Delay",
            FillWeight = 24
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Id",
            HeaderText = "Id",
            Visible = false
        });

        grid.SelectionChanged += (_, _) => SyncNodeActions();
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
        var menu = new ContextMenuStrip();
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
        _nodesGrid.Rows.Clear();

        var filteredNodes = GetFilteredNodes();

        foreach (var node in filteredNodes)
        {
            var rowIndex = _nodesGrid.Rows.Add(
                BuildNodeRoleText(node),
                node.Enabled ? "Enabled" : "Disabled",
                node.Name,
                node.Group,
                node.Server,
                node.ServerPort,
                _controller.GetNodeLatencyDisplay(node.Id),
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

    private void ApplyNodeRowStyle(DataGridViewRow row, NodeProfile node)
    {
        row.DefaultCellStyle.BackColor = Color.White;
        row.DefaultCellStyle.ForeColor = SystemColors.ControlText;

        if (!node.Enabled)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(247, 247, 247);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(120, 120, 120);
        }

        if (string.Equals(node.Id, _controller.Settings.SelectedNodeId, StringComparison.Ordinal))
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 250, 244);
            row.Cells["Selected"].Style.ForeColor = Color.FromArgb(27, 94, 32);
        }
        else if (string.Equals(node.Id, _controller.RuntimeState.CurrentRealNodeId, StringComparison.Ordinal))
        {
            row.Cells["Selected"].Style.ForeColor = Color.FromArgb(25, 118, 210);
        }

        row.Cells["Enabled"].Style.ForeColor = node.Enabled
            ? Color.FromArgb(27, 94, 32)
            : Color.FromArgb(164, 29, 55);
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
            MessageBox.Show(this, ex.Message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show(this, ex.Message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show(this, ex.Message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show(this, ex.Message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show(this, ex.Message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        await ExecuteAsync(async ct => _ = await _controller.ImportNodesFromTextAsync(dialog.SourceName, dialog.ImportContent, ct));
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
            MessageBox.Show(this, ex.Message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleActions(true);
            RefreshView();
        }
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
        _captureModeComboBox.Enabled = enabled;
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

        Color backColor;
        Color foreColor;
        string statusText;

        switch (runtimeState.CoreStatus)
        {
            case CoreStatus.Running:
                backColor = Color.FromArgb(223, 246, 221);
                foreColor = Color.FromArgb(27, 94, 32);
                statusText = "Connected";
                break;
            case CoreStatus.Starting:
                backColor = Color.FromArgb(255, 243, 205);
                foreColor = Color.FromArgb(133, 100, 4);
                statusText = "Connecting";
                break;
            case CoreStatus.Stopping:
                backColor = Color.FromArgb(255, 243, 205);
                foreColor = Color.FromArgb(133, 100, 4);
                statusText = "Disconnecting";
                break;
            case CoreStatus.Error:
                backColor = Color.FromArgb(253, 221, 230);
                foreColor = Color.FromArgb(164, 29, 55);
                statusText = "Connection Error";
                break;
            default:
                backColor = Color.FromArgb(236, 239, 241);
                foreColor = Color.FromArgb(55, 71, 79);
                statusText = "Disconnected";
                break;
        }

        _statusPanel.BackColor = backColor;
        _summaryLabel.ForeColor = foreColor;
        _statusLabel.ForeColor = foreColor;
        _statusDetailLabel.ForeColor = foreColor;
        _trafficLabel.ForeColor = foreColor;
        _statusLabel.Text = statusText;
        _statusDetailLabel.Text = BuildStatusDetail(runtimeState, selectedNodeName);
        _trafficLabel.Text = BuildTrafficDetail(runtimeState);
        Text = $"EasyNaive - {statusText}";
    }

    private string BuildHeaderSummary(RuntimeState runtimeState)
    {
        var parts = new List<string>
        {
            $"Stage: {runtimeState.StatusDetail}",
            $"Nodes: {_controller.Nodes.Count}",
            $"Subs: {_controller.Subscriptions.Count}"
        };

        if (runtimeState.ProcessId is int processId)
        {
            parts.Add($"PID: {processId}");
        }

        if (!string.IsNullOrWhiteSpace(runtimeState.ElevationStatusDetail))
        {
            parts.Add($"TUN: {runtimeState.ElevationStatusDetail}");
        }

        if (runtimeState.CurrentLatency is int latency)
        {
            parts.Add($"Latency: {latency} ms");
        }

        if (!string.IsNullOrWhiteSpace(runtimeState.LastError))
        {
            parts.Add($"Error: {runtimeState.LastError}");
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
            return runtimeState.LastError;
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
        _captureModeComboBox.SelectedItem = _controller.Settings.CaptureMode;
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

    private async void CaptureModeComboBoxOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressModeSelectionChanged || _captureModeComboBox.SelectedItem is not CaptureMode captureMode)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.SetCaptureModeAsync(captureMode, ct));
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
}
