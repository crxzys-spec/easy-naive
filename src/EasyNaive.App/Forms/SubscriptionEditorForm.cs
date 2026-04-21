using System.Drawing;
using System.Windows.Forms;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Forms;

internal sealed class SubscriptionEditorForm : Form
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _urlTextBox;
    private readonly CheckBox _enabledCheckBox;

    public SubscriptionEditorForm(SubscriptionProfile subscription)
    {
        Subscription = subscription;

        Text = string.IsNullOrWhiteSpace(subscription.Name) ? "Add Subscription" : $"Edit Subscription - {subscription.Name}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 210);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _nameTextBox = AddTextBox(layout, 0, "Name", subscription.Name);

        _urlTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = subscription.Url
        };
        layout.Controls.Add(CreateLabel("URL"), 0, 1);
        layout.Controls.Add(_urlTextBox, 1, 1);

        _enabledCheckBox = new CheckBox
        {
            Text = "Enabled",
            Checked = subscription.Enabled,
            AutoSize = true,
            Margin = new Padding(3, 7, 3, 0)
        };
        layout.Controls.Add(CreateLabel("Options"), 0, 2);
        layout.Controls.Add(_enabledCheckBox, 1, 2);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var saveButton = new Button
        {
            AutoSize = true,
            Text = "Save"
        };
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            AutoSize = true,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(cancelButton);
        layout.Controls.Add(new Panel(), 0, 3);
        layout.Controls.Add(buttonsPanel, 1, 3);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public SubscriptionProfile Subscription { get; }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            ShowValidationError("Subscription name is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_urlTextBox.Text))
        {
            ShowValidationError("Subscription URL is required.");
            return;
        }

        Subscription.Name = _nameTextBox.Text.Trim();
        Subscription.Url = _urlTextBox.Text.Trim();
        Subscription.Enabled = _enabledCheckBox.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }

    private static TextBox AddTextBox(TableLayoutPanel layout, int row, string label, string value)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = value
        };

        layout.Controls.Add(CreateLabel(label), 0, row);
        layout.Controls.Add(textBox, 1, row);
        return textBox;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(this, message, "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
