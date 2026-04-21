using System.Drawing;
using System.Windows.Forms;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Forms;

internal sealed class NodeEditorForm : Form
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _groupTextBox;
    private readonly TextBox _serverTextBox;
    private readonly NumericUpDown _portNumeric;
    private readonly TextBox _usernameTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly TextBox _tlsServerNameTextBox;
    private readonly TextBox _remarkTextBox;
    private readonly CheckBox _enabledCheckBox;
    private readonly CheckBox _quicCheckBox;
    private readonly CheckBox _udpOverTcpCheckBox;
    private readonly CheckBox _showPasswordCheckBox;

    public NodeEditorForm(NodeProfile nodeProfile)
    {
        NodeProfile = nodeProfile;

        Text = string.IsNullOrWhiteSpace(nodeProfile.Name) ? "Add Node" : $"Edit Node - {nodeProfile.Name}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 600);
        MinimumSize = new Size(640, 600);

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
            Padding = new Padding(0, 0, 0, 8),
            Text = "Define the node endpoint, credentials, and transport options used to build the sing-box outbound."
        };
        sectionsLayout.Controls.Add(introLabel, 0, 0);

        var basicGroup = CreateGroupBox("Basic");
        var basicLayout = CreateSectionLayout();
        _nameTextBox = AddTextBox(basicLayout, 0, "Name", nodeProfile.Name, "Friendly display name");
        _groupTextBox = AddTextBox(basicLayout, 1, "Group", nodeProfile.Group, "Default");
        _serverTextBox = AddTextBox(basicLayout, 2, "Server", nodeProfile.Server, "example.com");
        _portNumeric = AddNumeric(basicLayout, 3, "Port", nodeProfile.ServerPort);
        basicGroup.Controls.Add(basicLayout);
        sectionsLayout.Controls.Add(basicGroup, 0, 1);

        var authGroup = CreateGroupBox("Authentication");
        var authLayout = CreateSectionLayout();
        _usernameTextBox = AddTextBox(authLayout, 0, "Username", nodeProfile.Username, "Naive username");

        var passwordPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        passwordPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        passwordPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        _passwordTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = nodeProfile.Password,
            UseSystemPasswordChar = true
        };

        _showPasswordCheckBox = new CheckBox
        {
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0),
            Text = "Show password"
        };
        _showPasswordCheckBox.CheckedChanged += (_, _) =>
        {
            _passwordTextBox.UseSystemPasswordChar = !_showPasswordCheckBox.Checked;
        };

        passwordPanel.Controls.Add(_passwordTextBox, 0, 0);
        passwordPanel.Controls.Add(_showPasswordCheckBox, 0, 1);
        AddControlRow(authLayout, 1, "Password", passwordPanel);
        authGroup.Controls.Add(authLayout);
        sectionsLayout.Controls.Add(authGroup, 0, 2);

        var transportGroup = CreateGroupBox("Transport & TLS");
        var transportLayout = CreateSectionLayout();
        _tlsServerNameTextBox = AddTextBox(transportLayout, 0, "TLS SNI", nodeProfile.TlsServerName, "Leave blank to use the server host");

        var optionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 3, 0, 0),
            WrapContents = true
        };

        _enabledCheckBox = new CheckBox
        {
            Text = "Enabled",
            Checked = nodeProfile.Enabled,
            AutoSize = true
        };
        _quicCheckBox = new CheckBox
        {
            Text = "Use QUIC",
            Checked = nodeProfile.UseQuic,
            AutoSize = true
        };
        _udpOverTcpCheckBox = new CheckBox
        {
            Text = "UDP over TCP",
            Checked = nodeProfile.UseUdpOverTcp,
            AutoSize = true
        };

        optionsPanel.Controls.Add(_enabledCheckBox);
        optionsPanel.Controls.Add(_quicCheckBox);
        optionsPanel.Controls.Add(_udpOverTcpCheckBox);
        AddControlRow(transportLayout, 1, "Options", optionsPanel);

        var transportHintLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(90, 90, 90),
            Padding = new Padding(0, 6, 0, 0),
            Text = "QUIC and UDP over TCP depend on server support. Keep them off unless the node provider confirms they are available."
        };
        transportLayout.Controls.Add(new Panel(), 0, 2);
        transportLayout.Controls.Add(transportHintLabel, 1, 2);
        transportGroup.Controls.Add(transportLayout);
        sectionsLayout.Controls.Add(transportGroup, 0, 3);

        var notesGroup = CreateGroupBox("Notes");
        var notesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 10, 12, 12)
        };
        notesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        notesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _remarkTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 96,
            Text = nodeProfile.Remark,
            PlaceholderText = "Optional notes for this node"
        };

        var notesHintLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(90, 90, 90),
            Padding = new Padding(0, 6, 0, 0),
            Text = "Remark is local-only metadata and will not be sent to the server."
        };

        notesLayout.Controls.Add(_remarkTextBox, 0, 0);
        notesLayout.Controls.Add(notesHintLabel, 0, 1);
        notesGroup.Controls.Add(notesLayout);
        sectionsLayout.Controls.Add(notesGroup, 0, 4);

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

    public NodeProfile NodeProfile { get; }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            ShowValidationError("Node name is required.", _nameTextBox);
            return;
        }

        if (string.IsNullOrWhiteSpace(_serverTextBox.Text))
        {
            ShowValidationError("Server is required.", _serverTextBox);
            return;
        }

        NodeProfile.Name = _nameTextBox.Text.Trim();
        NodeProfile.Group = string.IsNullOrWhiteSpace(_groupTextBox.Text) ? "Default" : _groupTextBox.Text.Trim();
        NodeProfile.Server = _serverTextBox.Text.Trim();
        NodeProfile.ServerPort = decimal.ToInt32(_portNumeric.Value);
        NodeProfile.Username = _usernameTextBox.Text.Trim();
        NodeProfile.Password = _passwordTextBox.Text.Trim();
        NodeProfile.TlsServerName = _tlsServerNameTextBox.Text.Trim();
        NodeProfile.Remark = _remarkTextBox.Text.Trim();
        NodeProfile.Enabled = _enabledCheckBox.Checked;
        NodeProfile.UseQuic = _quicCheckBox.Checked;
        NodeProfile.UseUdpOverTcp = _udpOverTcpCheckBox.Checked;

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
            Padding = new Padding(10)
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static TextBox AddTextBox(TableLayoutPanel layout, int row, string label, string value, string placeholderText)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = value,
            PlaceholderText = placeholderText
        };

        AddControlRow(layout, row, label, textBox);
        return textBox;
    }

    private static NumericUpDown AddNumeric(TableLayoutPanel layout, int row, string label, int value)
    {
        var numeric = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Minimum = 1,
            Maximum = 65535,
            Width = 140,
            Value = Math.Clamp(value, 1, 65535)
        };

        AddControlRow(layout, row, label, numeric);
        return numeric;
    }

    private static void AddControlRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        while (layout.RowStyles.Count <= row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        control.Margin = new Padding(0, 3, 0, 0);
        layout.Controls.Add(CreateLabel(label), 0, row);
        layout.Controls.Add(control, 1, row);
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

    private void ShowValidationError(string message, Control targetControl)
    {
        MessageBox.Show(this, message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        targetControl.Focus();
    }
}
