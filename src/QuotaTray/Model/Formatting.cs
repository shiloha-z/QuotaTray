using System.Drawing;
using QuotaTray.Infra;

namespace QuotaTray.Model;

/// <summary>渲染格式化共享层（ADR-008）：剩余时间文案、百分比配色、调色板。
/// 历史：这些内容曾在 UsageSnapshot / DetailForm / TooltipForm / IconFactory 各存一份
/// 并发生漂移（如秒级格式有无不一），现收敛为单一来源。</summary>
internal static class Formatting
{
    // ---- 调色板（原 DetailForm / TooltipForm / IconFactory 各一份）----
    public static readonly Color ColorGreen = Color.FromArgb(46, 160, 67);
    public static readonly Color ColorYellow = Color.FromArgb(220, 130, 30);
    public static readonly Color ColorRed = Color.FromArgb(210, 50, 45);
    public static readonly Color TextTitle = Color.FromArgb(40, 40, 40);
    public static readonly Color TextLabel = Color.FromArgb(110, 110, 110);
    public static readonly Color TextReset = Color.FromArgb(150, 150, 150);
    public static readonly Color TextDim = Color.FromArgb(170, 170, 170);

    /// <summary>剩余时间文案（含秒级，随 1 秒倒计时刷新）。未知/非正 → “未知”。</summary>
    public static string FormatReset(long? seconds)
    {
        if (!seconds.HasValue || seconds.Value <= 0) return "未知";
        var t = TimeSpan.FromSeconds(seconds.Value);
        if (t.TotalDays >= 1) return $"{t.Days} 天 {t.Hours} 小时";
        if (t.TotalHours >= 1) return $"{t.Hours} 小时 {t.Minutes} 分";
        if (t.TotalMinutes >= 1) return $"{t.Minutes} 分 {t.Seconds} 秒";
        return $"{t.Seconds} 秒";
    }

    /// <summary>按设置阈值取百分比配色；null → 中性灰。</summary>
    public static Color PercentColor(double? percent, Settings settings)
    {
        if (!percent.HasValue) return TextReset;
        if (percent.Value < settings.WarningThresholdPercent) return ColorRed;
        if (percent.Value < settings.GreenThresholdPercent) return ColorYellow;
        return ColorGreen;
    }
}
