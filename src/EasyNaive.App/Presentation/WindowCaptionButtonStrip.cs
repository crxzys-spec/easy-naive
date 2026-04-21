using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class WindowCaptionButtonStrip : Control
{
    private const int ButtonCount = 3;

    private int _hoveredIndex = -1;
    private int _pressedIndex = -1;

    public WindowCaptionButtonStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.Opaque |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        Cursor = Cursors.Hand;
        Size = new Size(126, 38);
        Margin = Padding.Empty;
    }

    public event EventHandler? MinimizeClicked;

    public event EventHandler? MaximizeClicked;

    public event EventHandler? CloseClicked;

    public Color BaseColor { get; set; } = ModernTheme.BackgroundTop;

    public Color BackgroundStartColor { get; set; } = ModernTheme.BackgroundTop;

    public Color BackgroundEndColor { get; set; } = ModernTheme.BackgroundBottom;

    public LinearGradientMode BackgroundGradientMode { get; set; } = LinearGradientMode.Horizontal;

    public Size BackgroundCanvasSize { get; set; } = Size.Empty;

    public Point BackgroundOffset { get; set; } = Point.Empty;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hoveredIndex = HitTestButton(e.Location);
        if (_hoveredIndex == hoveredIndex)
        {
            return;
        }

        _hoveredIndex = hoveredIndex;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredIndex = -1;
        _pressedIndex = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _pressedIndex = HitTestButton(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var releasedIndex = HitTestButton(e.Location);
        var pressedIndex = _pressedIndex;
        _pressedIndex = -1;
        Invalidate();

        if (pressedIndex != releasedIndex)
        {
            return;
        }

        switch (releasedIndex)
        {
            case 0:
                MinimizeClicked?.Invoke(this, EventArgs.Empty);
                break;
            case 1:
                MaximizeClicked?.Invoke(this, EventArgs.Empty);
                break;
            case 2:
                CloseClicked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        PaintBaseBackground(e.Graphics);

        for (var index = 0; index < ButtonCount; index++)
        {
            var bounds = GetButtonBounds(index);
            if (_hoveredIndex == index || _pressedIndex == index)
            {
                using var path = CreateRoundRectanglePath(Rectangle.Inflate(bounds, -5, -6), 10);
                using var brush = new SolidBrush(GetOverlayColor(index));
                e.Graphics.FillPath(brush, path);
            }

            DrawGlyph(e.Graphics, bounds, index);
        }
    }

    private void PaintBaseBackground(Graphics graphics)
    {
        if (BackgroundCanvasSize.Width <= 0 || BackgroundCanvasSize.Height <= 0)
        {
            graphics.Clear(BaseColor);
            return;
        }

        var gradientBounds = new Rectangle(
            -BackgroundOffset.X,
            -BackgroundOffset.Y,
            Math.Max(Width, BackgroundCanvasSize.Width),
            Math.Max(Height, BackgroundCanvasSize.Height));
        using var brush = new LinearGradientBrush(
            gradientBounds,
            BackgroundStartColor,
            BackgroundEndColor,
            BackgroundGradientMode);
        graphics.FillRectangle(brush, new Rectangle(-1, -1, Width + 2, Height + 2));
    }

    private void DrawGlyph(Graphics graphics, Rectangle bounds, int index)
    {
        using var pen = new Pen(GetGlyphColor(index), 1.8F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var centerX = bounds.Left + bounds.Width / 2F;
        var centerY = bounds.Top + bounds.Height / 2F;
        switch (index)
        {
            case 0:
                graphics.DrawLine(pen, centerX - 6, centerY + 4, centerX + 6, centerY + 4);
                break;
            case 1:
                graphics.DrawRectangle(pen, centerX - 6, centerY - 5, 12, 10);
                break;
            case 2:
                graphics.DrawLine(pen, centerX - 5, centerY - 5, centerX + 5, centerY + 5);
                graphics.DrawLine(pen, centerX + 5, centerY - 5, centerX - 5, centerY + 5);
                break;
        }
    }

    private int HitTestButton(Point point)
    {
        if (!ClientRectangle.Contains(point))
        {
            return -1;
        }

        return Math.Clamp(point.X / Math.Max(1, Width / ButtonCount), 0, ButtonCount - 1);
    }

    private Rectangle GetButtonBounds(int index)
    {
        var baseWidth = Width / ButtonCount;
        var left = index * baseWidth;
        var width = index == ButtonCount - 1 ? Width - left : baseWidth;
        return new Rectangle(left, 0, width, Height);
    }

    private Color GetOverlayColor(int index)
    {
        if (index == 2)
        {
            return _pressedIndex == index ? ModernTheme.Danger : ModernTheme.DangerSoft;
        }

        return _pressedIndex == index
            ? ControlPaint.Dark(BaseColor, 0.08F)
            : Color.FromArgb(230, 241, 246);
    }

    private Color GetGlyphColor(int index)
    {
        return index == 2 && _pressedIndex == index ? Color.White : index == 2 ? ModernTheme.Danger : ModernTheme.Text;
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
