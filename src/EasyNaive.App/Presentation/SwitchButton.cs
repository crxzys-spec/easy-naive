using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class SwitchButton : Control
{
    private bool _checked;

    public SwitchButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint,
            true);
        Cursor = Cursors.Hand;
        Size = new Size(128, 34);
        Font = ModernTheme.BodyFont;
        BackColor = Color.Transparent;
    }

    public event EventHandler? CheckedChanged;

    public string OffText { get; set; } = "Off";

    public string OnText { get; set; } = "On";

    public Color AccentColor { get; set; } = ModernTheme.Accent;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        PaintResolvedParentBackground(e.Graphics);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var backgroundPath = CreateRoundRectanglePath(bounds, Height / 2);
        using var backgroundBrush = new SolidBrush(Color.FromArgb(230, 241, 246));
        using var borderPen = new Pen(ModernTheme.Border);
        e.Graphics.FillPath(backgroundBrush, backgroundPath);
        e.Graphics.DrawPath(borderPen, backgroundPath);

        var halfWidth = (Width - 8) / 2;
        var thumbBounds = Checked
            ? new Rectangle(Width - halfWidth - 4, 4, halfWidth, Height - 8)
            : new Rectangle(4, 4, halfWidth, Height - 8);
        using var thumbPath = CreateRoundRectanglePath(thumbBounds, thumbBounds.Height / 2);
        using var thumbBrush = new SolidBrush(AccentColor);
        e.Graphics.FillPath(thumbBrush, thumbPath);

        var dividerX = Width / 2;
        using (var dividerPen = new Pen(Color.FromArgb(204, 220, 230)))
        {
            e.Graphics.DrawLine(dividerPen, dividerX, 8, dividerX, Height - 8);
        }

        var offBounds = new Rectangle(4, 0, halfWidth, Height);
        var onBounds = new Rectangle(Width - halfWidth - 4, 0, halfWidth, Height);
        TextRenderer.DrawText(
            e.Graphics,
            OffText,
            Font,
            offBounds,
            Checked ? ModernTheme.MutedText : Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            e.Graphics,
            OnText,
            Font,
            onBounds,
            Checked ? Color.White : ModernTheme.MutedText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void PaintResolvedParentBackground(Graphics graphics)
    {
        graphics.Clear(GetResolvedParentBackColor());
    }

    private Color GetResolvedParentBackColor()
    {
        for (Control? current = Parent; current is not null; current = current.Parent)
        {
            if (current.BackColor.A > 0)
            {
                return current.BackColor;
            }
        }

        return ModernTheme.BackgroundBottom;
    }

    private static GraphicsPath CreateRoundRectanglePath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
