using System.Drawing;
using System.Windows.Forms;

namespace EasyNaive.App.Forms;

internal sealed class SubscriptionManagerForm : Form
{
    private readonly CoreController _controller;
    private readonly Button _addButton;
    private readonly Button _editButton;
    private readonly Button _refreshButton;
    private readonly Button _refreshAllButton;
    private readonly Button _deleteButton;
    private readonly DataGridView _subscriptionsGrid;

    public SubscriptionManagerForm(CoreController controller)
    {
        _controller = controller;

        Text = "Subscriptions";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 420);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8),
            WrapContents = false
        };

        _addButton = CreateActionButton("Add", AddSubscriptionAsync);
        _editButton = CreateActionButton("Edit", EditSubscriptionAsync);
        _refreshButton = CreateActionButton("Refresh Selected", RefreshSelectedAsync);
        _refreshAllButton = CreateActionButton("Refresh All", RefreshAllAsync);
        _deleteButton = CreateActionButton("Delete", DeleteSelectedAsync);

        actionPanel.Controls.Add(_addButton);
        actionPanel.Controls.Add(_editButton);
        actionPanel.Controls.Add(_refreshButton);
        actionPanel.Controls.Add(_refreshAllButton);
        actionPanel.Controls.Add(_deleteButton);

        _subscriptionsGrid = CreateGrid();

        Controls.Add(_subscriptionsGrid);
        Controls.Add(actionPanel);

        RefreshView();
    }

    public void RefreshView()
    {
        var selectedId = GetSelectedSubscriptionId();
        _subscriptionsGrid.Rows.Clear();

        foreach (var subscription in _controller.Subscriptions)
        {
            _subscriptionsGrid.Rows.Add(
                subscription.Enabled,
                subscription.Name,
                subscription.ImportedNodeCount,
                subscription.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
                string.IsNullOrWhiteSpace(subscription.LastError) ? "OK" : subscription.LastError,
                subscription.Url,
                subscription.Id);
        }

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            SelectRowBySubscriptionId(selectedId);
        }

        SyncActions();
    }

    private static Button CreateActionButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text
        };
        button.Click += async (_, _) => await action();
        return button;
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
            BackgroundColor = SystemColors.Window,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Enabled",
            HeaderText = "On",
            FillWeight = 12
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Name",
            FillWeight = 22
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Nodes",
            HeaderText = "Nodes",
            FillWeight = 12
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Updated",
            HeaderText = "Updated",
            FillWeight = 20
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            FillWeight = 20
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

        await ExecuteAsync(ct => _controller.AddSubscriptionAsync(dialog.Subscription, ct));
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

        await ExecuteAsync(ct => _controller.UpdateSubscriptionAsync(dialog.Subscription, ct));
    }

    private async Task RefreshSelectedAsync()
    {
        var subscriptionId = GetSelectedSubscriptionId();
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        await ExecuteAsync(ct => _controller.RefreshSubscriptionAsync(subscriptionId, ct));
    }

    private async Task RefreshAllAsync()
    {
        await ExecuteAsync(_controller.RefreshAllSubscriptionsAsync);
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

    private string? GetSelectedSubscriptionId()
    {
        if (_subscriptionsGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        return _subscriptionsGrid.SelectedRows[0].Cells["Id"].Value?.ToString();
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
        var hasSelection = !string.IsNullOrWhiteSpace(GetSelectedSubscriptionId());
        _editButton.Enabled = hasSelection;
        _refreshButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _refreshAllButton.Enabled = _controller.Subscriptions.Count > 0;
    }

    private void ToggleActions(bool enabled)
    {
        _addButton.Enabled = enabled;
        _refreshAllButton.Enabled = enabled && _controller.Subscriptions.Count > 0;

        if (!enabled)
        {
            _editButton.Enabled = false;
            _refreshButton.Enabled = false;
            _deleteButton.Enabled = false;
            _refreshAllButton.Enabled = false;
            return;
        }

        SyncActions();
    }
}
