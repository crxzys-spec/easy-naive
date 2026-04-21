using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class ActionButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public ActionButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        TabStop = false;
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

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _pressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        PaintResolvedParentBackground(pevent.Graphics);

        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var fillColor = Enabled ? BackColor : ModernTheme.SurfaceMuted;
        if (Enabled && _pressed)
        {
            fillColor = ControlPaint.Dark(fillColor, 0.08f);
        }
        else if (Enabled && _hovered)
        {
            fillColor = ControlPaint.Light(fillColor, 0.12f);
        }

        using var path = CreateRoundRectanglePath(bounds, 14);
        using var fillBrush = new SolidBrush(fillColor);
        pevent.Graphics.FillPath(fillBrush, path);

        using var borderPen = new Pen(GetBorderColor(fillColor), 1f);
        pevent.Graphics.DrawPath(borderPen, path);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            ClientRectangle,
            Enabled ? ForeColor : ModernTheme.Neutral,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void PaintResolvedParentBackground(Graphics graphics)
    {
        var gradientParent = FindGradientParent();
        if (gradientParent is not null)
        {
            var offset = GetRelativeLocation(this, gradientParent);
            var gradientBounds = new Rectangle(
                -offset.X,
                -offset.Y,
                Math.Max(Width, gradientParent.ClientSize.Width),
                Math.Max(Height, gradientParent.ClientSize.Height));
            using var brush = new LinearGradientBrush(
                gradientBounds,
                gradientParent.StartColor,
                gradientParent.EndColor,
                gradientParent.GradientMode);
            graphics.FillRectangle(brush, ClientRectangle);
            return;
        }

        graphics.Clear(GetResolvedParentBackColor());
    }

    private GradientPanel? FindGradientParent()
    {
        for (Control? current = Parent; current is not null; current = current.Parent)
        {
            if (current is GradientPanel gradientPanel)
            {
                return gradientPanel;
            }
        }

        return null;
    }

    private static Point GetRelativeLocation(Control control, Control ancestor)
    {
        var location = Point.Empty;
        for (Control? current = control; current is not null && !ReferenceEquals(current, ancestor); current = current.Parent)
        {
            location.Offset(current.Left, current.Top);
        }

        return location;
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

    private Color GetBorderColor(Color fillColor)
    {
        if (!Enabled)
        {
            return Color.FromArgb(198, 213, 224);
        }

        if (_pressed)
        {
            return Color.FromArgb(118, 158, 187);
        }

        if (_hovered)
        {
            return Color.FromArgb(130, 177, 208);
        }

        return fillColor.GetBrightness() > 0.82f
            ? Color.FromArgb(176, 203, 220)
            : ControlPaint.Dark(fillColor, 0.14f);
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
