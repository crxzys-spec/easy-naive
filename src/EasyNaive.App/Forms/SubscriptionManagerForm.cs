using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EasyNaive.App.Diagnostics;
using EasyNaive.App.Presentation;
using EasyNaive.Core.Models;

namespace EasyNaive.App.Forms;

internal sealed class SubscriptionManagerForm : Form
{
    private readonly CoreController _controller;
    private readonly Button _addButton;
    private readonly Button _editButton;
    private readonly Button _toggleEnabledButton;
    private readonly Button _refreshButton;
    private readonly Button _refreshAllButton;
    private readonly Button _deleteButton;
    private readonly DataGridView _subscriptionsGrid;
    private readonly Label _summaryLabel;
    private readonly HashSet<string> _refreshingSubscriptionIds = new(StringComparer.Ordinal);

    public SubscriptionManagerForm(CoreController controller)
    {
        _controller = controller;

        Text = "Subscriptions";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 500);
        BackColor = ModernTheme.BackgroundBottom;
        Font = ModernTheme.BodyFont;

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 10, 14, 6),
            WrapContents = true,
            BackColor = ModernTheme.BackgroundBottom
        };

        _addButton = CreateActionButton("Add", AddSubscriptionAsync);
        _editButton = CreateActionButton("Edit", EditSubscriptionAsync);
        _toggleEnabledButton = CreateActionButton("Disable", ToggleSelectedEnabledAsync);
        _refreshButton = CreateActionButton("Refresh Selected", RefreshSelectedAsync);
        _refreshAllButton = CreateActionButton("Refresh All", RefreshAllAsync);
        _deleteButton = CreateActionButton("Delete", DeleteSelectedAsync);

        actionPanel.Controls.Add(_addButton);
        actionPanel.Controls.Add(_editButton);
        actionPanel.Controls.Add(_toggleEnabledButton);
        actionPanel.Controls.Add(_refreshButton);
        actionPanel.Controls.Add(_refreshAllButton);
        actionPanel.Controls.Add(_deleteButton);

        _summaryLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Padding = new Padding(18, 6, 18, 0),
            BackColor = ModernTheme.BackgroundBottom,
            ForeColor = ModernTheme.MutedText,
            Font = ModernTheme.SmallFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _subscriptionsGrid = CreateGrid();

        Controls.Add(_subscriptionsGrid);
        Controls.Add(_summaryLabel);
        Controls.Add(actionPanel);

        RefreshView();
    }

    public void RefreshView()
    {
        var selectedId = GetSelectedSubscriptionId();
        _subscriptionsGrid.Rows.Clear();

        foreach (var subscription in _controller.Subscriptions)
        {
            var rowIndex = _subscriptionsGrid.Rows.Add(
                BuildEnabledDisplay(subscription),
                subscription.Name,
                subscription.ImportedNodeCount,
                BuildStatusDisplay(subscription),
                FormatTimestamp(subscription.LastUpdated),
                BuildDurationDisplay(subscription),
                string.IsNullOrWhiteSpace(subscription.LastError) ? "-" : ErrorMessageTranslator.ToDisplayMessage(subscription.LastError),
                subscription.Url,
                subscription.Id);

            ApplyRowStyle(_subscriptionsGrid.Rows[rowIndex], subscription);
        }

        _summaryLabel.Text = BuildSummaryText();

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            SelectRowBySubscriptionId(selectedId);
        }

        SyncActions();
    }

    private static Button CreateActionButton(string text, Func<Task> action)
    {
        var colors = GetActionButtonColors(text);
        var button = new Button
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
            Text = text
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(colors.BackColor, 0.12f);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(colors.BackColor, 0.08f);
        ApplyRoundedRegion(button, 14);
        button.Resize += (_, _) => ApplyRoundedRegion(button, 14);
        button.Click += async (_, _) => await action();
        return button;
    }

    private static (Color BackColor, Color ForeColor) GetActionButtonColors(string text)
    {
        return text switch
        {
            "Refresh All" => (ModernTheme.Accent, Color.White),
            "Refresh Selected" => (ModernTheme.MintSoft, Color.FromArgb(28, 122, 58)),
            "Delete" => (ModernTheme.DangerSoft, ModernTheme.Danger),
            "Disable" => (ModernTheme.WarningSoft, Color.FromArgb(143, 92, 9)),
            "Enable" => (ModernTheme.Mint, Color.White),
            _ => (ModernTheme.SurfaceStrong, ModernTheme.Text)
        };
    }

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
            Name = "Enabled",
            HeaderText = "State",
            FillWeight = 13
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Name",
            FillWeight = 24
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Nodes",
            HeaderText = "Nodes",
            FillWeight = 10
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            FillWeight = 16
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Updated",
            HeaderText = "Updated",
            FillWeight = 22
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Duration",
            HeaderText = "Duration",
            FillWeight = 13
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Error",
            HeaderText = "Error",
            FillWeight = 30
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Url",
            HeaderText = "URL",
            FillWeight = 34
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Id",
            Visible = false
        });

        grid.SelectionChanged += (_, _) => SyncActions();
        grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                await EditSubscriptionAsync();
            }
        };

        return grid;
    }

    private async Task AddSubscriptionAsync()
    {
        using var dialog = new SubscriptionEditorForm(_controller.CreateSubscriptionDraft());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithRefreshingStateAsync(
            dialog.Subscription.Enabled ? new[] { dialog.Subscription.Id } : Array.Empty<string>(),
            ct => _controller.AddSubscriptionAsync(dialog.Subscription, ct));
    }

    private async Task EditSubscriptionAsync()
    {
        var subscriptionId = GetSelectedSubscriptionId();
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        using var dialog = new SubscriptionEditorForm(_controller.CreateSubscriptionDraft(subscriptionId));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await ExecuteWithRefreshingStateAsync(
            dialog.Subscription.Enabled ? new[] { dialog.Subscription.Id } : Array.Empty<string>(),
            ct => _controller.UpdateSubscriptionAsync(dialog.Subscription, ct));
    }

    private async Task ToggleSelectedEnabledAsync()
    {
        var subscription = GetSelectedSubscription();
        if (subscription is null)
        {
            return;
        }

        var targetEnabled = !subscription.Enabled;
        await ExecuteWithRefreshingStateAsync(
            targetEnabled ? new[] { subscription.Id } : Array.Empty<string>(),
            ct => _controller.SetSubscriptionEnabledAsync(subscription.Id, targetEnabled, ct));
    }

    private async Task RefreshSelectedAsync()
    {
        var subscription = GetSelectedSubscription();
        if (subscription is null)
        {
            return;
        }

        await ExecuteWithRefreshingStateAsync(
            new[] { subscription.Id },
            ct => _controller.RefreshSubscriptionAsync(subscription.Id, ct));
    }

    private async Task RefreshAllAsync()
    {
        var refreshingIds = _controller.Subscriptions
            .Where(subscription => subscription.Enabled)
            .Select(subscription => subscription.Id)
            .ToArray();

        await ExecuteWithRefreshingStateAsync(refreshingIds, _controller.RefreshAllSubscriptionsAsync);
    }

    private async Task DeleteSelectedAsync()
    {
        var subscriptionId = GetSelectedSubscriptionId();
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        var subscription = _controller.Subscriptions.FirstOrDefault(current => current.Id == subscriptionId);
        var message = subscription is null
            ? "Delete this subscription?"
            : $"Delete subscription \"{subscription.Name}\" and all imported nodes?";

        if (MessageBox.Show(this, message, "EasyNaive", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        await ExecuteAsync(ct => _controller.RemoveSubscriptionAsync(subscriptionId, ct));
    }

    private async Task ExecuteWithRefreshingStateAsync(IEnumerable<string> refreshingIds, Func<CancellationToken, Task> action)
    {
        var ids = refreshingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var id in ids)
        {
            _refreshingSubscriptionIds.Add(id);
        }

        RefreshView();

        try
        {
            await ExecuteAsync(action);
        }
        finally
        {
            foreach (var id in ids)
            {
                _refreshingSubscriptionIds.Remove(id);
            }

            RefreshView();
        }
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

    private string? GetSelectedSubscriptionId()
    {
        if (_subscriptionsGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        return _subscriptionsGrid.SelectedRows[0].Cells["Id"].Value?.ToString();
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

    private SubscriptionProfile? GetSelectedSubscription()
    {
        var subscriptionId = GetSelectedSubscriptionId();
        return string.IsNullOrWhiteSpace(subscriptionId)
            ? null
            : _controller.Subscriptions.FirstOrDefault(current => string.Equals(current.Id, subscriptionId, StringComparison.Ordinal));
    }

    private void SelectRowBySubscriptionId(string subscriptionId)
    {
        foreach (DataGridViewRow row in _subscriptionsGrid.Rows)
        {
            var rowId = row.Cells["Id"].Value?.ToString();
            row.Selected = string.Equals(rowId, subscriptionId, StringComparison.Ordinal);
        }
    }

    private void SyncActions()
    {
        var selectedSubscription = GetSelectedSubscription();
        var hasSelection = selectedSubscription is not null;
        var isRefreshingSelection = hasSelection && _refreshingSubscriptionIds.Contains(selectedSubscription!.Id);

        _editButton.Enabled = hasSelection && !isRefreshingSelection;
        _toggleEnabledButton.Enabled = hasSelection && !isRefreshingSelection;
        _refreshButton.Enabled = hasSelection && selectedSubscription!.Enabled && !isRefreshingSelection;
        _deleteButton.Enabled = hasSelection && !isRefreshingSelection;
        _refreshAllButton.Enabled = _controller.Subscriptions.Any(subscription => subscription.Enabled);

        if (hasSelection)
        {
            _toggleEnabledButton.Text = selectedSubscription!.Enabled ? "Disable" : "Enable";
            var colors = GetActionButtonColors(_toggleEnabledButton.Text);
            _toggleEnabledButton.BackColor = colors.BackColor;
            _toggleEnabledButton.ForeColor = colors.ForeColor;
            _toggleEnabledButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(colors.BackColor, 0.12f);
            _toggleEnabledButton.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(colors.BackColor, 0.08f);
        }
        else
        {
            _toggleEnabledButton.Text = "Disable";
        }
    }

    private void ToggleActions(bool enabled)
    {
        _addButton.Enabled = enabled;
        _refreshAllButton.Enabled = enabled && _controller.Subscriptions.Any(subscription => subscription.Enabled);

        if (!enabled)
        {
            _editButton.Enabled = false;
            _toggleEnabledButton.Enabled = false;
            _refreshButton.Enabled = false;
            _deleteButton.Enabled = false;
            _refreshAllButton.Enabled = false;
            return;
        }

        SyncActions();
    }

    private void ApplyRowStyle(DataGridViewRow row, SubscriptionProfile subscription)
    {
        row.DefaultCellStyle.BackColor = ModernTheme.SurfaceStrong;
        row.DefaultCellStyle.ForeColor = ModernTheme.Text;
        row.DefaultCellStyle.SelectionBackColor = ModernTheme.AccentDark;
        row.DefaultCellStyle.SelectionForeColor = Color.White;

        if (!subscription.Enabled)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(244, 247, 249);
            row.DefaultCellStyle.ForeColor = ModernTheme.Neutral;
            row.Cells["Enabled"].Style.ForeColor = ModernTheme.Neutral;
            row.Cells["Status"].Style.ForeColor = ModernTheme.Neutral;
            return;
        }

        row.Cells["Enabled"].Style.ForeColor = ModernTheme.Mint;
        row.Cells["Enabled"].Style.Font = ModernTheme.SectionFont;

        if (_refreshingSubscriptionIds.Contains(subscription.Id))
        {
            row.DefaultCellStyle.BackColor = ModernTheme.WarningSoft;
            row.Cells["Status"].Style.ForeColor = ModernTheme.Warning;
            row.Cells["Status"].Style.Font = ModernTheme.SectionFont;
            return;
        }

        if (!string.IsNullOrWhiteSpace(subscription.LastError))
        {
            row.DefaultCellStyle.BackColor = ModernTheme.DangerSoft;
            row.Cells["Status"].Style.ForeColor = ModernTheme.Danger;
            row.Cells["Status"].Style.Font = ModernTheme.SectionFont;
            row.Cells["Error"].Style.ForeColor = ModernTheme.Danger;
            return;
        }

        row.Cells["Status"].Style.ForeColor = subscription.LastUpdated is null
            ? ModernTheme.Neutral
            : ModernTheme.Mint;
        row.Cells["Status"].Style.Font = ModernTheme.SectionFont;
    }

    private string BuildSummaryText()
    {
        var totalCount = _controller.Subscriptions.Count;
        var enabledCount = _controller.Subscriptions.Count(subscription => subscription.Enabled);
        var failedCount = _controller.Subscriptions.Count(subscription => !string.IsNullOrWhiteSpace(subscription.LastError));
        var refreshingCount = _refreshingSubscriptionIds.Count;

        return $"Subscriptions: {totalCount} | Enabled: {enabledCount} | Failed: {failedCount} | Updating: {refreshingCount}";
    }

    private string BuildStatusDisplay(SubscriptionProfile subscription)
    {
        if (_refreshingSubscriptionIds.Contains(subscription.Id))
        {
            return "Updating";
        }

        if (!subscription.Enabled)
        {
            return "Disabled";
        }

        if (!string.IsNullOrWhiteSpace(subscription.LastError))
        {
            return "Failed";
        }

        return subscription.LastUpdated is null ? "Never updated" : "OK";
    }

    private static string BuildEnabledDisplay(SubscriptionProfile subscription)
    {
        return subscription.Enabled ? "\u25CF Enabled" : "\u25CF Disabled";
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    }

    private string BuildDurationDisplay(SubscriptionProfile subscription)
    {
        if (_refreshingSubscriptionIds.Contains(subscription.Id))
        {
            return "...";
        }

        if (subscription.LastRefreshDurationMilliseconds <= 0)
        {
            return "-";
        }

        return subscription.LastRefreshDurationMilliseconds < 1000
            ? $"{subscription.LastRefreshDurationMilliseconds} ms"
            : $"{subscription.LastRefreshDurationMilliseconds / 1000.0:0.0} s";
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
}
