using System.Drawing;
using System.Windows.Forms;
using EasyNaive.App.Importing;
using EasyNaive.App.Presentation;

namespace EasyNaive.App.Forms;

internal sealed class ManualImportPreviewForm : Form
{
    private readonly ManualImportPreview _preview;
    private readonly DataGridView _nodesGrid;
    private readonly CheckBox _skipDuplicatesCheckBox;
    private readonly Label _summaryLabel;
    private readonly Button _importButton;

    public ManualImportPreviewForm(ManualImportPreview preview)
    {
        _preview = preview;

        Text = "Import Preview";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 560);
        BackColor = ModernTheme.BackgroundBottom;
        Font = ModernTheme.BodyFont;
        ShowInTaskbar = false;

        _summaryLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 0),
            BackColor = ModernTheme.BackgroundBottom,
            ForeColor = ModernTheme.Text,
            Font = ModernTheme.SectionFont
        };

        _nodesGrid = CreateGrid();
        PopulateGrid();

        _skipDuplicatesCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = preview.DuplicateCount > 0,
            Enabled = preview.DuplicateCount > 0,
            Text = "Skip duplicate nodes",
            ForeColor = ModernTheme.Text,
            Padding = new Padding(0, 8, 0, 0)
        };
        _skipDuplicatesCheckBox.CheckedChanged += (_, _) => UpdateSummary();

        _importButton = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(96, 34),
            Text = "Import",
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(96, 34),
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        };

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            ColumnCount = 2,
            Padding = new Padding(16, 8, 16, 10),
            BackColor = ModernTheme.BackgroundBottom
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        buttonsPanel.Controls.Add(_importButton);
        buttonsPanel.Controls.Add(cancelButton);

        bottomPanel.Controls.Add(_skipDuplicatesCheckBox, 0, 0);
        bottomPanel.Controls.Add(buttonsPanel, 1, 0);

        Controls.Add(_nodesGrid);
        Controls.Add(bottomPanel);
        Controls.Add(_summaryLabel);

        AcceptButton = _importButton;
        CancelButton = cancelButton;
        UpdateSummary();
    }

    public bool SkipDuplicates => _skipDuplicatesCheckBox.Checked;

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
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
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 34 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ModernTheme.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ModernTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = ModernTheme.SectionFont;
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
            Name = "Status",
            HeaderText = "Status",
            FillWeight = 16
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Name",
            FillWeight = 30
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Group",
            HeaderText = "Group",
            FillWeight = 22
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Server",
            HeaderText = "Server",
            FillWeight = 32
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Port",
            HeaderText = "Port",
            FillWeight = 12
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Tls",
            HeaderText = "TLS/SNI",
            FillWeight = 28
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Reason",
            HeaderText = "Reason",
            FillWeight = 26
        });

        return grid;
    }

    private void PopulateGrid()
    {
        _nodesGrid.Rows.Clear();

        foreach (var item in _preview.Items)
        {
            var node = item.Node;
            var rowIndex = _nodesGrid.Rows.Add(
                item.IsDuplicate ? "Duplicate" : "New",
                node.Name,
                node.Group,
                node.Server,
                node.ServerPort,
                node.TlsServerName,
                item.DuplicateReason);

            var row = _nodesGrid.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = item.IsDuplicate ? ModernTheme.WarningSoft : ModernTheme.SurfaceStrong;
            row.Cells["Status"].Style.ForeColor = item.IsDuplicate ? ModernTheme.Warning : ModernTheme.Mint;
            row.Cells["Status"].Style.Font = ModernTheme.SectionFont;
            row.Cells["Reason"].Style.ForeColor = item.IsDuplicate ? ModernTheme.Warning : ModernTheme.Neutral;
        }
    }

    private void UpdateSummary()
    {
        var importCount = _skipDuplicatesCheckBox.Checked ? _preview.NewCount : _preview.TotalCount;
        _summaryLabel.Text = $"Source: {_preview.SourceName} | Total: {_preview.TotalCount} | New: {_preview.NewCount} | Duplicates: {_preview.DuplicateCount} | Will import: {importCount}";
        _importButton.Text = importCount == 1 ? "Import 1 node" : $"Import {importCount} nodes";
        _importButton.Enabled = importCount > 0;
    }
}
