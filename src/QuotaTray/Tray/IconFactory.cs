using System.Drawing.Drawing2D;

namespace QuotaTray.Tray;

internal static class IconFactory
{
    private static readonly Dictionary<Color, Icon> Cache = new();

    public static readonly Color Green = Color.FromArgb(46, 204, 64);
    public static readonly Color Yellow = Color.FromArgb(255, 133, 27);
    public static readonly Color Red = Color.FromArgb(255, 65, 54);
    public static readonly Color Gray = Color.FromArgb(160, 160, 160);

    public static Icon Get(Color color)
    {
        if (Cache.TryGetValue(color, out var icon))
        {
            return icon;
        }

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var pen = new Pen(Color.FromArgb(60, 60, 60), 1.5f);
            g.DrawEllipse(pen, 2, 2, 28, 28);
        }

        icon = Icon.FromHandle(bitmap.GetHicon());
        Cache[color] = icon;
        return icon;
    }
}
