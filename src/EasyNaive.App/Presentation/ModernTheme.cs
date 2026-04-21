using System.Drawing;

namespace EasyNaive.App.Presentation;

internal static class ModernTheme
{
    public static readonly Color BackgroundTop = Color.FromArgb(236, 248, 250);
    public static readonly Color BackgroundBottom = Color.FromArgb(246, 248, 252);
    public static readonly Color Surface = Color.FromArgb(244, 250, 252);
    public static readonly Color SurfaceStrong = Color.FromArgb(255, 255, 255);
    public static readonly Color SurfaceMuted = Color.FromArgb(235, 243, 247);
    public static readonly Color Border = Color.FromArgb(209, 224, 232);
    public static readonly Color GridLine = Color.FromArgb(225, 234, 240);

    public static readonly Color Text = Color.FromArgb(24, 38, 53);
    public static readonly Color MutedText = Color.FromArgb(84, 103, 119);

    public static readonly Color Accent = Color.FromArgb(0, 122, 255);
    public static readonly Color AccentDark = Color.FromArgb(26, 79, 134);
    public static readonly Color Mint = Color.FromArgb(52, 199, 89);
    public static readonly Color MintSoft = Color.FromArgb(222, 247, 228);
    public static readonly Color Warning = Color.FromArgb(245, 166, 35);
    public static readonly Color WarningSoft = Color.FromArgb(255, 244, 214);
    public static readonly Color Danger = Color.FromArgb(210, 60, 78);
    public static readonly Color DangerSoft = Color.FromArgb(252, 228, 234);
    public static readonly Color Neutral = Color.FromArgb(116, 128, 140);

    public static readonly Font TitleFont = new("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font SectionFont = new("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font BodyFont = new("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font SmallFont = new("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
}
