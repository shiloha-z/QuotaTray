using System.Text.Json;
using QuotaTray.Infra;
using QuotaTray.Model;

namespace QuotaTray.Sources;

internal sealed class ChatGptUsage
{
    private readonly HiddenFetchWebView _web = new("chatgpt", "https://chatgpt.com/");

    public void ResetSession() => _web.Dispose();

    public async Task<(SourceStatus Status, string Detail, double? Percent, long? ResetSec)> FetchAsync(
        string endpoint, string jsonPath, double? maxValue, bool valueIsRemaining, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(jsonPath))
        {
            return (SourceStatus.NotConfigured, "未配置端点（请先重新登录）", null, null);
        }

        try
        {
            var result = await _web.FetchAsync(endpoint, "{}", ct);

            if (result.Status is 401 or 403)
            {
                Logger.Log($"ChatGPT fetch status {result.Status} on {endpoint}: {Truncate(result.Body)}");
                return (SourceStatus.AuthFailed, "登录失效，请重新登录", null, null);
            }

            if (result.Status == 0)
            {
                Logger.Log($"ChatGPT fetch status 0 on {endpoint}: {Truncate(result.Body)}");
                return (SourceStatus.Error, "查询超时", null, null);
            }

            if (result.Status != 200)
            {
                Logger.Log($"ChatGPT HTTP {result.Status} on {endpoint}: {Truncate(result.Body)}");
                return (SourceStatus.Error, $"HTTP {result.Status}", null, null);
            }

            using var doc = JsonDocument.Parse(result.Body);
            var element = JsonPath.Get(doc.RootElement, jsonPath);
            if (element is null || element.Value.ValueKind != JsonValueKind.Number)
            {
                Logger.Log($"ChatGPT jsonPath mismatch on {endpoint}: {Truncate(result.Body)}");
                try
                {
                    File.WriteAllText(Path.Combine(AppPaths.DataDir, "chatgpt_probe_last.json"), result.Body);
                }
                catch
                {
                }

                return (SourceStatus.Error, "响应结构不匹配，请重新登录", null, null);
            }

            var value = element.Value.GetDouble();
            double? percent = null;
            var detail = value.ToString("0.##");

            if (maxValue.HasValue && maxValue.Value > 0)
            {
                percent = Math.Min(100, value / maxValue.Value * 100);
                var remaining = valueIsRemaining ? value : maxValue.Value - value;
                detail = $"周限额剩余 {Math.Max(0, remaining) / maxValue.Value * 100:0}%";
            }

            long? resetSec = null;
            var dotIndex = jsonPath.LastIndexOf('.');
            if (dotIndex > 0)
            {
                var resetPath = jsonPath[..dotIndex] + ".reset_after_seconds";
                var resetElement = JsonPath.Get(doc.RootElement, resetPath);
                if (resetElement is { ValueKind: JsonValueKind.Number })
                {
                    resetSec = (long)resetElement.Value.GetDouble();
                }
            }

            return (SourceStatus.Ok, detail, percent, resetSec);
        }
        catch (TaskCanceledException)
        {
            return (SourceStatus.Error, "请求超时", null, null);
        }
        catch (Exception ex)
        {
            Logger.Log("ChatGPT fetch error: " + ex.Message);
            return (SourceStatus.Error, ex.Message, null, null);
        }
    }

    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..2000];
}
