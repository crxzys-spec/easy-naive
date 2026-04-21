using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class GradientPanel : Panel
{
    public GradientPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    public Color StartColor { get; set; } = ModernTheme.BackgroundTop;

    public Color EndColor { get; set; } = ModernTheme.BackgroundBottom;

    public LinearGradientMode GradientMode { get; set; } = LinearGradientMode.Horizontal;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            base.OnPaintBackground(e);
            return;
        }

        using var brush = new LinearGradientBrush(ClientRectangle, StartColor, EndColor, GradientMode);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
