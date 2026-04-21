using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class SearchBox : UserControl
{
    private readonly TextBox _textBox;
    private bool _hovered;

    public SearchBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        Width = 320;
        Height = 32;
        BackColor = Color.Transparent;
        Cursor = Cursors.IBeam;
        TabStop = false;

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(248, 252, 255),
            ForeColor = ModernTheme.Text,
            Font = ModernTheme.BodyFont,
            TabStop = true
        };
        _textBox.TextChanged += (_, _) =>
        {
            Invalidate();
            OnTextChanged(EventArgs.Empty);
        };
        _textBox.GotFocus += (_, _) => Invalidate();
        _textBox.LostFocus += (_, _) => Invalidate();
        Controls.Add(_textBox);
    }

    public string PlaceholderText
    {
        get => _textBox.PlaceholderText;
        set => _textBox.PlaceholderText = value;
    }

    [AllowNull]
    public override string Text
    {
        get => _textBox.Text;
        set => _textBox.Text = value ?? string.Empty;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _textBox.Font = Font;
        LayoutTextBox();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        _textBox.ForeColor = ForeColor;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutTextBox();
    }

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
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _textBox.Focus();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _textBox.Focus();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(GetParentBackColor());
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        var focused = _textBox.Focused;
        var fillColor = Color.FromArgb(248, 252, 255);
        var borderColor = focused
            ? ModernTheme.Accent
            : _hovered
                ? Color.FromArgb(154, 191, 216)
                : Color.FromArgb(198, 219, 232);

        using var path = CreateRoundRectanglePath(bounds, 15.5f);
        using var fillBrush = new SolidBrush(fillColor);
        using var borderPen = new Pen(borderColor, focused ? 1.4f : 1f);
        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        DrawSearchGlyph(e.Graphics, focused ? ModernTheme.Accent : ModernTheme.Neutral);
    }

    private void LayoutTextBox()
    {
        if (_textBox is null || _textBox.IsDisposed || Width <= 0 || Height <= 0)
        {
            return;
        }

        const int left = 34;
        const int right = 12;
        var preferredHeight = _textBox.PreferredHeight;
        _textBox.BackColor = Color.FromArgb(248, 252, 255);
        _textBox.SetBounds(
            left,
            Math.Max(1, (Height - preferredHeight) / 2),
            Math.Max(10, Width - left - right),
            preferredHeight);
    }

    private void DrawSearchGlyph(Graphics graphics, Color color)
    {
        using var pen = new Pen(color, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var centerY = Height / 2f;
        var circle = new RectangleF(14f, centerY - 5.5f, 9f, 9f);
        graphics.DrawEllipse(pen, circle);
        graphics.DrawLine(pen, 21f, centerY + 3f, 25.5f, centerY + 7.5f);
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
