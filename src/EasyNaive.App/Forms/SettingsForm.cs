using System.Drawing;
using System.Windows.Forms;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Forms;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _mixedPortNumeric;
    private readonly NumericUpDown _clashApiPortNumeric;
    private readonly ComboBox _logLevelComboBox;
    private readonly CheckBox _autoStartCheckBox;
    private readonly CheckBox _minimizeToTrayCheckBox;
    private readonly CheckBox _tunStrictRouteCheckBox;

    public SettingsForm(AppSettings settings)
    {
        Settings = settings;

        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 520);
        MinimumSize = new Size(700, 520);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        var sectionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1
        };

        var introLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            MaximumSize = new Size(620, 0),
            Padding = new Padding(0, 0, 0, 8),
            Text = "Adjust local ports and runtime behavior. Port, log level, and TUN route changes restart the core when already connected."
        };
        sectionsLayout.Controls.Add(introLabel, 0, 0);

        var portsGroup = CreateGroupBox("Ports");
        var portsLayout = CreateSectionLayout();
        _mixedPortNumeric = AddNumeric(portsLayout, 0, "Mixed Port", settings.ProxyMixedPort, "Local HTTP/SOCKS mixed inbound port used in Proxy mode.");
        _clashApiPortNumeric = AddNumeric(portsLayout, 1, "Clash API Port", settings.ClashApiPort, "Local controller port used by the tray app.");
        portsGroup.Controls.Add(portsLayout);
        sectionsLayout.Controls.Add(portsGroup, 0, 1);

        var behaviorGroup = CreateGroupBox("Behavior");
        var behaviorLayout = CreateSectionLayout();
        _autoStartCheckBox = AddCheckBox(behaviorLayout, 0, "Launch at startup", settings.EnableAutoStart, "Register EasyNaive in Windows startup.");
        _minimizeToTrayCheckBox = AddCheckBox(behaviorLayout, 1, "Minimize to tray", settings.EnableMinimizeToTray, "Hide the window when minimized.");
        _tunStrictRouteCheckBox = AddCheckBox(behaviorLayout, 2, "TUN strict route", settings.EnableTunStrictRoute, "Use stricter route handling in TUN mode. Some software may be affected.");
        behaviorGroup.Controls.Add(behaviorLayout);
        sectionsLayout.Controls.Add(behaviorGroup, 0, 2);

        var diagnosticsGroup = CreateGroupBox("Diagnostics");
        var diagnosticsLayout = CreateSectionLayout();
        _logLevelComboBox = new ComboBox
        {
            Dock = DockStyle.Left,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140
        };
        _logLevelComboBox.Items.AddRange(["debug", "info", "warn", "error"]);
        _logLevelComboBox.SelectedItem = string.IsNullOrWhiteSpace(settings.LogLevel)
            ? "info"
            : settings.LogLevel.Trim().ToLowerInvariant();
        AddControlRow(diagnosticsLayout, 0, "Log Level", _logLevelComboBox, "Controls the verbosity written to sing-box and app logs.");
        diagnosticsGroup.Controls.Add(diagnosticsLayout);
        sectionsLayout.Controls.Add(diagnosticsGroup, 0, 3);

        contentPanel.Controls.Add(sectionsLayout);
        rootLayout.Controls.Add(contentPanel, 0, 0);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0)
        };

        var saveButton = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(90, 32),
            Text = "Save"
        };
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(90, 32),
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(cancelButton);
        rootLayout.Controls.Add(buttonsPanel, 0, 1);

        Controls.Add(rootLayout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public AppSettings Settings { get; }

    private void SaveAndClose()
    {
        var mixedPort = decimal.ToInt32(_mixedPortNumeric.Value);
        var clashApiPort = decimal.ToInt32(_clashApiPortNumeric.Value);

        if (mixedPort == clashApiPort)
        {
            MessageBox.Show(this, "Mixed port and Clash API port must be different.", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _clashApiPortNumeric.Focus();
            return;
        }

        Settings.ProxyMixedPort = mixedPort;
        Settings.ClashApiPort = clashApiPort;
        Settings.EnableAutoStart = _autoStartCheckBox.Checked;
        Settings.EnableMinimizeToTray = _minimizeToTrayCheckBox.Checked;
        Settings.EnableTunStrictRoute = _tunStrictRouteCheckBox.Checked;
        Settings.LogLevel = _logLevelComboBox.SelectedItem?.ToString() ?? "info";

        DialogResult = DialogResult.OK;
        Close();
    }

    private static GroupBox CreateGroupBox(string text)
    {
        return new GroupBox
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(10),
            Text = text
        };
    }

    private static TableLayoutPanel CreateSectionLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 10, 12, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static NumericUpDown AddNumeric(TableLayoutPanel layout, int row, string label, int value, string hint)
    {
        var numeric = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Minimum = 1,
            Maximum = 65535,
            Width = 140,
            Value = Math.Clamp(value, 1, 65535)
        };

        AddControlRow(layout, row, label, numeric, hint);
        return numeric;
    }

    private static CheckBox AddCheckBox(TableLayoutPanel layout, int row, string label, bool isChecked, string hint)
    {
        var checkBox = new CheckBox
        {
            AutoSize = true,
            Checked = isChecked,
            Margin = new Padding(0, 6, 0, 0),
            Text = label
        };

        AddControlRow(layout, row, string.Empty, checkBox, hint, spanBothColumns: true);
        return checkBox;
    }

    private static void AddControlRow(TableLayoutPanel layout, int row, string label, Control control, string hint, bool spanBothColumns = false)
    {
        while (layout.RowStyles.Count <= row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(control, 0, 0);
        host.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(90, 90, 90),
            MaximumSize = new Size(480, 0),
            Padding = new Padding(0, 4, 0, 0),
            Text = hint
        }, 0, 1);

        if (spanBothColumns)
        {
            layout.Controls.Add(host, 0, row);
            layout.SetColumnSpan(host, 2);
            return;
        }

        layout.Controls.Add(CreateLabel(label), 0, row);
        layout.Controls.Add(host, 1, row);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 6, 0, 0),
            Text = text,
            TextAlign = ContentAlignment.TopLeft
        };
    }
}
