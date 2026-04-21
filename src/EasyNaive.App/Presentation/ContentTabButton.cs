using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class ContentTabButton : Control
{
    private bool _hovered;
    private bool _pressed;
    private bool _selected;

    public ContentTabButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);

        Width = 132;
        Height = 34;
        Margin = new Padding(0, 0, 8, 0);
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            Font = _selected ? ModernTheme.SectionFont : ModernTheme.BodyFont;
            Invalidate();
        }
    }

    protected override bool ShowFocusCues => false;

    protected override bool ShowKeyboardCues => false;

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

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(GetParentBackColor());
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 5));
        if (Selected || _hovered || _pressed)
        {
            using var path = CreateRoundRectanglePath(bounds, 12.5f);
            using var fillBrush = new SolidBrush(GetFillColor());
            e.Graphics.FillPath(fillBrush, path);

            if (Selected)
            {
                using var borderPen = new Pen(Color.FromArgb(200, 221, 235), 1f);
                e.Graphics.DrawPath(borderPen, path);
            }
        }

        if (Selected)
        {
            using var indicatorPen = new Pen(ModernTheme.Accent, 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            var indicatorY = Height - 3.5f;
            e.Graphics.DrawLine(indicatorPen, Width / 2f - 19f, indicatorY, Width / 2f + 19f, indicatorY);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(0, 0, Width, Math.Max(1, Height - 4)),
            Selected ? ModernTheme.AccentDark : ModernTheme.MutedText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private Color GetFillColor()
    {
        if (Selected)
        {
            return _pressed ? Color.FromArgb(232, 243, 251) : ModernTheme.SurfaceStrong;
        }

        if (_pressed)
        {
            return Color.FromArgb(224, 237, 245);
        }

        return Color.FromArgb(237, 246, 251);
    }

    private Color GetParentBackColor()
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
        var diameter = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
