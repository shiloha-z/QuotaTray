using System.Drawing.Drawing2D;
using System.Drawing.Text;
using QuotaTray.Model;

namespace QuotaTray.Tray;

internal static class IconFactory
{
    // 调色板统一引用 Formatting（ADR-008）；Gray 为图标置灰态专用。
    public static readonly Color Green = Formatting.ColorGreen;
    public static readonly Color Yellow = Formatting.ColorYellow;
    public static readonly Color Red = Formatting.ColorRed;
    public static readonly Color Gray = Color.FromArgb(150, 150, 150);

    private static Icon? _single;

    /// <summary>获取固定样式图标：白色圆角方块底 + 斜体黑色 Q。color/percent 仅用于签名兼容。</summary>
    public static Icon Get(Color color, double? percent)
    {
        return _single ??= CreateIcon();
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // 白色圆角方块底
            var rect = new Rectangle(2, 2, 28, 28);
            using var path = RoundedRect(rect, 7);

            using (var brush = new SolidBrush(Color.White))
            {
                g.FillPath(brush, path);
            }

            // 细灰描边，保证白底在浅色任务栏上可见
            using var pen = new Pen(Color.FromArgb(180, 180, 180), 1f);
            g.DrawPath(pen, path);

            // 衬线斜体浅蓝 Q（字号调小防止在托盘小尺寸下被裁切尾巴）
            using var font = new Font("Georgia", 14f, FontStyle.Italic | FontStyle.Bold);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            using var qBrush = new SolidBrush(Color.FromArgb(136, 182, 210));
            g.DrawString("Q", font, qBrush, rect, sf);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}