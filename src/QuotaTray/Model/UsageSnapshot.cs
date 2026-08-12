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

    /// <summary>本次快照刷新时刻（本地时间），用于实时倒计时计算。</summary>
    public DateTime RefreshedAt { get; set; } = DateTime.Now;

    public double? OverallPercent
    {
        get
        {
            // 统一为“剩余百分比”后取最小值（Go 的字段是已用%，需 100-转剩余）
            double?[] raw =
            {
                ChatGptPercent,
                Go5hPercent.HasValue ? 100 - Go5hPercent.Value : null,
                GoWeekPercent.HasValue ? 100 - GoWeekPercent.Value : null,
                GoMonthPercent.HasValue ? 100 - GoMonthPercent.Value : null,
            };
            var values = raw
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToArray();
            return values.Length == 0 ? null : values.Min();
        }
    }

}
