using System.Drawing;
using System.Windows.Forms;

namespace EasyNaive.App.Forms;

internal sealed class ManualImportForm : Form
{
    private readonly TextBox _sourceNameTextBox;
    private readonly TextBox _contentTextBox;

    public ManualImportForm(string sourceName, string content)
    {
        SourceName = sourceName;
        ImportContent = content;

        Text = "Import Nodes";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 520);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _sourceNameTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = sourceName
        };
        layout.Controls.Add(CreateLabel("Source"), 0, 0);
        layout.Controls.Add(_sourceNameTextBox, 1, 0);

        _contentTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10.0f),
            Text = content
        };
        layout.Controls.Add(CreateLabel("Content"), 0, 1);
        layout.Controls.Add(_contentTextBox, 1, 1);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var importButton = new Button
        {
            AutoSize = true,
            Text = "Import"
        };
        importButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            AutoSize = true,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        };

        buttonsPanel.Controls.Add(importButton);
        buttonsPanel.Controls.Add(cancelButton);

        layout.Controls.Add(new Panel(), 0, 2);
        layout.Controls.Add(buttonsPanel, 1, 2);

        Controls.Add(layout);
        AcceptButton = importButton;
        CancelButton = cancelButton;
    }

    public string SourceName { get; private set; }

    public string ImportContent { get; private set; }

    private void SaveAndClose()
    {
        var sourceName = _sourceNameTextBox.Text.Trim();
        var content = _contentTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            ShowValidationError("Source name is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            ShowValidationError("Import content is required.");
            return;
        }

        SourceName = sourceName;
        ImportContent = content;

        DialogResult = DialogResult.OK;
        Close();
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
