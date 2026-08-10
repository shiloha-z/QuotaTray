using System.Text.RegularExpressions;
using QuotaTray.Infra;
using QuotaTray.Model;

namespace QuotaTray.Sources;

internal sealed class GoUsage
{
    private readonly HiddenFetchWebView _web = new("zen", "https://opencode.ai/");

    public void ResetSession() => _web.Dispose();

    public async Task<(SourceStatus Status, string Detail, double? Pct5h, double? PctWeek, double? PctMonth,
        long? Reset5hSec, long? ResetWeekSec, long? ResetMonthSec)> FetchAsync(
        string workspaceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return (SourceStatus.NotConfigured, "未登录（请先重新登录 Go）", null, null, null, null, null, null);
        }

        var endpoint = $"https://opencode.ai/workspace/{workspaceId}/go";
        try
        {
            var result = await _web.FetchAsync(endpoint, "{}", ct);

            if (result.Status is 401 or 403)
            {
                Logger.Log($"Go fetch status {result.Status} on {endpoint}");
                return (SourceStatus.AuthFailed, "登录失效，请重新登录", null, null, null, null, null, null);
            }

            if (result.Status == 0)
            {
                Logger.Log($"Go fetch status 0 on {endpoint}: {Truncate(result.Body)}");
                return (SourceStatus.Error, "查询超时", null, null, null, null, null, null);
            }

            if (result.Status != 200)
            {
                Logger.Log($"Go HTTP {result.Status} on {endpoint}: {Truncate(result.Body)}");
                return (SourceStatus.Error, $"HTTP {result.Status}", null, null, null, null, null, null);
            }

            var rolling = ParseWindow(result.Body, "rollingUsage");
            var weekly = ParseWindow(result.Body, "weeklyUsage");
            var monthly = ParseWindow(result.Body, "monthlyUsage");

            if (rolling is null || weekly is null || monthly is null)
            {
                Logger.Log($"Go usage windows not found on {endpoint}, body len={result.Body.Length}");
                return (SourceStatus.Error, "页面无用量数据", null, null, null, null, null, null);
            }

            Logger.Log($"Go usage OK: 5h={rolling.Value.Percent}% (reset {rolling.Value.ResetSec}s) " +
                       $"week={weekly.Value.Percent}% (reset {weekly.Value.ResetSec}s) " +
                       $"month={monthly.Value.Percent}% (reset {monthly.Value.ResetSec}s)");
            return (SourceStatus.Ok, "OK",
                rolling.Value.Percent, weekly.Value.Percent, monthly.Value.Percent,
                rolling.Value.ResetSec, weekly.Value.ResetSec, monthly.Value.ResetSec);
        }
        catch (TaskCanceledException)
        {
            return (SourceStatus.Error, "请求超时", null, null, null, null, null, null);
        }
        catch (Exception ex)
        {
            Logger.Log("Go fetch error: " + ex.Message);
            return (SourceStatus.Error, "查询失败（请查看日志）", null, null, null, null, null, null);
        }
    }

    private static (int Percent, long ResetSec)? ParseWindow(string html, string name)
    {
        var match = Regex.Match(html,
            $@"{name}:\$R\[\d+\]=\{{status:""[^""]*"",resetInSec:(\d+),usagePercent:(\d+)\}}");
        if (!match.Success)
        {
            return null;
        }

        return (int.Parse(match.Groups[2].Value), long.Parse(match.Groups[1].Value));
    }

    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..2000];
}
