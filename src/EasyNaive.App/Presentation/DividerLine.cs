using System.Drawing;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class DividerLine : Control
{
    public DividerLine()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        Height = 1;
        Margin = Padding.Empty;
        TabStop = false;
    }

    public Color LineColor { get; set; } = Color.FromArgb(222, 233, 240);

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(LineColor, 1f);
        var y = Math.Max(0, Height / 2);
        e.Graphics.DrawLine(pen, 0, y, Width, y);
    }
}
