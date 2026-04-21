using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal enum WindowCaptionButtonKind
{
    Minimize,
    Maximize,
    Close
}

internal sealed class WindowCaptionButton : Control
{
    private bool _hovered;
    private bool _pressed;

    public WindowCaptionButton(WindowCaptionButtonKind kind)
    {
        Kind = kind;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        Cursor = Cursors.Hand;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Size = new Size(42, 34);
    }

    public WindowCaptionButtonKind Kind { get; }

    public Color BaseColor { get; set; } = ModernTheme.BackgroundTop;

    public Color BackgroundStartColor { get; set; } = ModernTheme.BackgroundTop;

    public Color BackgroundEndColor { get; set; } = ModernTheme.BackgroundBottom;

    public LinearGradientMode BackgroundGradientMode { get; set; } = LinearGradientMode.Horizontal;

    public Size BackgroundCanvasSize { get; set; } = Size.Empty;

    public Point BackgroundOffset { get; set; } = Point.Empty;

    public Color GlyphColor { get; set; } = ModernTheme.Text;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        PaintBaseBackground(e.Graphics);

        if (_hovered || _pressed)
        {
            var bounds = new Rectangle(4, 5, Width - 8, Height - 10);
            using var path = CreateRoundRectanglePath(bounds, 10);
            using var brush = new SolidBrush(GetOverlayColor());
            e.Graphics.FillPath(brush, path);
        }

        using var pen = new Pen(GetGlyphColor(), 1.8F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var centerX = Width / 2F;
        var centerY = Height / 2F;
        switch (Kind)
        {
            case WindowCaptionButtonKind.Minimize:
                e.Graphics.DrawLine(pen, centerX - 6, centerY + 4, centerX + 6, centerY + 4);
                break;
            case WindowCaptionButtonKind.Maximize:
                e.Graphics.DrawRectangle(pen, centerX - 6, centerY - 5, 12, 10);
                break;
            case WindowCaptionButtonKind.Close:
                e.Graphics.DrawLine(pen, centerX - 5, centerY - 5, centerX + 5, centerY + 5);
                e.Graphics.DrawLine(pen, centerX + 5, centerY - 5, centerX - 5, centerY + 5);
                break;
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
        graphics.FillRectangle(brush, ClientRectangle);
    }

    private Color GetOverlayColor()
    {
        if (Kind == WindowCaptionButtonKind.Close)
        {
            return _pressed ? ModernTheme.Danger : ModernTheme.DangerSoft;
        }

        return _pressed
            ? ControlPaint.Dark(BaseColor, 0.08F)
            : Color.FromArgb(230, 241, 246);
    }

    private Color GetGlyphColor()
    {
        return Kind == WindowCaptionButtonKind.Close && _pressed ? Color.White : GlyphColor;
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
