namespace QuotaTray.Model;

internal enum SourceStatus
{
    NotConfigured,
    Ok,
    AuthFailed,
    Error,
}

internal sealed class UsageSnapshot
{
    public SourceStatus ChatGptStatus { get; set; } = SourceStatus.NotConfigured;
    public string ChatGptDetail { get; set; } = "未登录";
    public double? ChatGptPercent { get; set; }
    public long? ChatGptResetSec { get; set; }

    public SourceStatus GoStatus { get; set; } = SourceStatus.NotConfigured;
    public string GoDetail { get; set; } = "未登录";
    public double? Go5hPercent { get; set; }
    public double? GoWeekPercent { get; set; }
    public double? GoMonthPercent { get; set; }
    public long? GoReset5hSec { get; set; }
    public long? GoResetWeekSec { get; set; }
    public long? GoResetMonthSec { get; set; }

    public bool HasError =>
        ChatGptStatus is SourceStatus.Error or SourceStatus.AuthFailed
        || GoStatus is SourceStatus.Error or SourceStatus.AuthFailed;

    public double? OverallPercent
    {
        get
        {
            var values = new[] { ChatGptPercent, Go5hPercent, GoWeekPercent, GoMonthPercent }
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToArray();
            return values.Length == 0 ? null : values.Min();
        }
    }

    public string TooltipText
    {
        get
        {
            var lines = new List<string> { "ChatGPT Plus" };
            if (ChatGptResetSec.HasValue)
            {
                lines.Add($"  {ChatGptDetail}  重置于 {FormatReset(ChatGptResetSec)}");
            }
            else
            {
                lines.Add("  " + ChatGptDetail);
            }

            lines.Add("");
            if (GoStatus == SourceStatus.Ok &&
                Go5hPercent.HasValue && GoWeekPercent.HasValue && GoMonthPercent.HasValue)
            {
                lines.Add("opencode Go");
                lines.Add($"  5h滚动  剩余 {100 - Go5hPercent.Value:0}%   重置于 {FormatReset(GoReset5hSec)}");
                lines.Add($"  每周    剩余 {100 - GoWeekPercent.Value:0}%   重置于 {FormatReset(GoResetWeekSec)}");
                lines.Add($"  每月    剩余 {100 - GoMonthPercent.Value:0}%   重置于 {FormatReset(GoResetMonthSec)}");
            }
            else
            {
                lines.Add("opencode Go");
                lines.Add("  " + GoDetail);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string FormatReset(long? seconds)
    {
        if (!seconds.HasValue || seconds.Value <= 0)
        {
            return "未知";
        }

        var t = TimeSpan.FromSeconds(seconds.Value);
        if (t.TotalDays >= 1)
        {
            return $"{t.Days}天{t.Hours}小时";
        }

        if (t.TotalHours >= 1)
        {
            return $"{t.Hours}小时{t.Minutes}分";
        }

        return $"{t.Minutes}分";
    }
}
