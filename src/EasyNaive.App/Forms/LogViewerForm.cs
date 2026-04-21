using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EasyNaive.App.Diagnostics;
using EasyNaive.App.Presentation;

namespace EasyNaive.App.Forms;

internal sealed class LogViewerForm : Form
{
    private const int MaxReadBytes = 1024 * 1024;
    private static readonly Regex AnsiEscapeSequenceRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex NonPrintableControlRegex = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);

    private readonly CoreController _controller;
    private readonly ComboBox _logSelectorComboBox;
    private readonly RichTextBox _logTextBox;
    private readonly Label _statusLabel;

    public LogViewerForm(CoreController controller)
    {
        _controller = controller;

        Text = "Logs";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(960, 620);
        BackColor = ModernTheme.BackgroundBottom;
        Font = ModernTheme.BodyFont;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(14, 10, 14, 6),
            WrapContents = false,
            BackColor = ModernTheme.BackgroundBottom
        };

        _logSelectorComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Width = 180,
            BackColor = ModernTheme.SurfaceStrong,
            ForeColor = ModernTheme.Text
        };
        _logSelectorComboBox.Items.AddRange(["App Log", "sing-box Log"]);
        _logSelectorComboBox.SelectedIndex = 0;
        _logSelectorComboBox.SelectedIndexChanged += (_, _) => RefreshLog();

        toolbar.Controls.Add(_logSelectorComboBox);
        toolbar.Controls.Add(CreateButton("Refresh", () => RefreshLog()));
        toolbar.Controls.Add(CreateButton("Copy Last Error", CopyLastError));
        toolbar.Controls.Add(CreateButton("Open Folder", _controller.OpenLogsDirectory));

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Padding = new Padding(16, 5, 16, 0),
            ForeColor = ModernTheme.MutedText,
            BackColor = ModernTheme.BackgroundBottom,
            Font = ModernTheme.SmallFont
        };

        _logTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(18, 26, 34),
            ForeColor = Color.FromArgb(220, 232, 240),
            Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            WordWrap = false,
            DetectUrls = false
        };

        Controls.Add(_logTextBox);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);

        RefreshLog();
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(0, 34),
            Padding = new Padding(12, 3, 12, 3),
            Margin = new Padding(6, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = ModernTheme.SurfaceStrong,
            ForeColor = ModernTheme.Text,
            Font = ModernTheme.BodyFont,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Text = text
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => action();
        return button;
    }

    private void RefreshLog()
    {
        var path = GetSelectedLogPath();
        try
        {
            var content = ReadTail(path, MaxReadBytes, out var truncated);
            content = SanitizeLogForDisplay(content);
            RenderLog(content);
            _statusLabel.Text = BuildStatusText(path, content.Length, truncated);
        }
        catch (Exception ex)
        {
            _logTextBox.Clear();
            _logTextBox.SelectionColor = ModernTheme.Danger;
            _logTextBox.AppendText(ErrorMessageTranslator.ToDisplayMessage(ex));
            _statusLabel.Text = path;
        }
    }

    private string GetSelectedLogPath()
    {
        return _logSelectorComboBox.SelectedIndex == 1
            ? _controller.SingBoxLogPath
            : _controller.AppLogPath;
    }

    private static string ReadTail(string path, int maxBytes, out bool truncated)
    {
        truncated = false;
        if (!File.Exists(path))
        {
            return $"Log file does not exist yet: {path}";
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length <= maxBytes)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        truncated = true;
        stream.Seek(-maxBytes, SeekOrigin.End);
        var buffer = new byte[maxBytes];
        var read = stream.Read(buffer, 0, buffer.Length);
        var text = Encoding.UTF8.GetString(buffer, 0, read);
        var firstNewLine = text.IndexOf('\n');
        return firstNewLine >= 0 ? text[(firstNewLine + 1)..] : text;
    }

    private static string SanitizeLogForDisplay(string content)
    {
        var withoutAnsi = AnsiEscapeSequenceRegex.Replace(content, string.Empty);
        return NonPrintableControlRegex.Replace(withoutAnsi, string.Empty);
    }

    private void RenderLog(string content)
    {
        _logTextBox.SuspendLayout();
        _logTextBox.Clear();

        var lines = content.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            _logTextBox.SelectionColor = GetLineColor(line);
            _logTextBox.AppendText(line);
            _logTextBox.AppendText(Environment.NewLine);
        }

        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.ScrollToCaret();
        _logTextBox.ResumeLayout();
    }

    private static Color GetLineColor(string line)
    {
        if (line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains(" ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromArgb(255, 126, 126);
        }

        if (line.Contains("[WARN]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains(" WARN", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromArgb(255, 205, 112);
        }

        if (line.Contains("[INFO]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains(" INFO", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromArgb(185, 215, 235);
        }

        return Color.FromArgb(220, 232, 240);
    }

    private void CopyLastError()
    {
        var lines = _logTextBox.Lines;
        var lastError = lines
            .Reverse()
            .FirstOrDefault(line =>
                line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("WARN", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(lastError))
        {
            MessageBox.Show(this, "No ERROR, FATAL, or WARN line found in the visible log.", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Clipboard.SetText(lastError);
        MessageBox.Show(this, "Copied last error line.", "EasyNaive", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string BuildStatusText(string path, int visibleCharacters, bool truncated)
    {
        var prefix = truncated ? "Showing last 1 MB" : "Showing full file";
        return $"{prefix} | Visible chars: {visibleCharacters:N0} | {path}";
    }
}
