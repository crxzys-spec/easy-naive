using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyNaive.App.Presentation;

internal sealed class ModernTabControl : TabControl
{
    public ModernTabControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(132, 34);
        Padding = new Point(10, 4);
        Font = ModernTheme.BodyFont;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabPages.Count)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ModernTheme.BackgroundBottom);

        var tabBounds = GetTabRect(e.Index);
        tabBounds.Inflate(-4, -5);
        var selected = e.Index == SelectedIndex;

        using var tabPath = CreateRoundRectanglePath(tabBounds, 13);
        using var tabBrush = new SolidBrush(selected ? ModernTheme.Accent : Color.FromArgb(234, 242, 247));
        using var borderPen = new Pen(selected ? ModernTheme.Accent : ModernTheme.Border);
        e.Graphics.FillPath(tabBrush, tabPath);
        e.Graphics.DrawPath(borderPen, tabPath);

        TextRenderer.DrawText(
            e.Graphics,
            TabPages[e.Index].Text,
            Font,
            tabBounds,
            selected ? Color.White : ModernTheme.MutedText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(ModernTheme.BackgroundBottom);
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
