using QuotaTray.Infra;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace QuotaTray.Auth;

internal enum LoginKind
{
    ChatGpt,
    Zen,
}

internal sealed class LoginWindow : Form
{
    private readonly LoginKind _kind;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly Button _doneButton = new() { Text = "完成并保存登录态", Dock = DockStyle.Bottom, Height = 44 };
    private readonly Label _statusLabel = new()
    {
        Dock = DockStyle.Top,
        Height = 36,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 8, 0),
    };
    private readonly Label _counterLabel = new()
    {
        Dock = DockStyle.Top,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 8, 0),
        ForeColor = Color.DimGray,
    };
    private string? _bearer;
    private string? _apiKey;
    private string? _orgId;
    private int _capturedCount;

    public LoginWindow(LoginKind kind)
    {
        _kind = kind;
        Text = kind == LoginKind.ChatGpt ? "登录 ChatGPT (Agent Usage Checker)" : "登录 OpenCode Zen (Agent Usage Checker)";
        Width = 980;
        Height = 740;
        StartPosition = FormStartPosition.CenterScreen;
        _doneButton.Font = new Font("Segoe UI", 10f);
        _statusLabel.Font = new Font("Segoe UI", 9f);
        _counterLabel.Font = new Font("Segoe UI", 8f);
        Controls.Add(_web);
        Controls.Add(_doneButton);
        Controls.Add(_statusLabel);
        Controls.Add(_counterLabel);
        _doneButton.Click += async (_, _) => await SaveAsync();
        Shown += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            var profileDir = Path.Combine(AppPaths.WebViewDataDir, _kind == LoginKind.ChatGpt ? "chatgpt" : "zen");
            var environment = await CoreWebView2Environment.CreateAsync(null, profileDir);
            await _web.EnsureCoreWebView2Async(environment);

            _web.CoreWebView2.SourceChanged += (_, _) =>
            {
                Logger.Log("NAV " + _kind + " -> " + _web.CoreWebView2.Source);
                _statusLabel.Text = "当前页面: " + _web.CoreWebView2.Source;
            };

            if (_kind == LoginKind.ChatGpt)
            {
                _web.CoreWebView2.AddWebResourceRequestedFilter(
                    "https://chatgpt.com/backend-api/*", CoreWebView2WebResourceContext.All);
                _web.CoreWebView2.WebResourceRequested += (_, e) => CaptureRequest("chatgpt", e.Request.Uri, null);
                _web.CoreWebView2.Navigate("https://chatgpt.com/");
                _statusLabel.Text = "在窗口中登录 ChatGPT。登录后请打开一次用量/设置页面，然后点下方按钮。";
            }
            else
            {
                _web.CoreWebView2.AddWebResourceRequestedFilter(
                    "https://*.opencode.ai/*", CoreWebView2WebResourceContext.All);
                _web.CoreWebView2.AddWebResourceRequestedFilter(
                    "https://opencode.ai/*", CoreWebView2WebResourceContext.All);
                _web.CoreWebView2.WebResourceRequested += (_, e) =>
                {
                    var uri = e.Request.Uri;
                    var auth = e.Request.Headers.GetHeader("Authorization");
                    var apiKey = e.Request.Headers.GetHeader("x-opencode-api-key");
                    var orgId = e.Request.Headers.GetHeader("x-org-id");
                    CaptureRequest("zen", uri, auth);
                    if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        _bearer = auth["Bearer ".Length..].Trim();
                    }

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        _apiKey = apiKey.Trim();
                    }

                    if (!string.IsNullOrEmpty(orgId))
                    {
                        _orgId = orgId.Trim();
                    }
                };
                _web.CoreWebView2.Navigate("https://opencode.ai/auth");
                _statusLabel.Text = "在窗口中用 GitHub/Google 登录。登录后会自动跳到控制台 (console.opencode.ai)，看到用量页面后点下方按钮保存。";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "WebView2 初始化失败: " + ex.Message;
            Logger.Log("LoginWindow init error: " + ex);
        }
    }

    private void CaptureRequest(string source, string uri, string? auth)
    {
        _capturedCount++;
        _counterLabel.Text = $"已捕获 {_capturedCount} 个请求 -> {AppPaths.CaptureFile}";
        var line = $"{DateTime.Now:HH:mm:ss} {source} {uri}" + (string.IsNullOrEmpty(auth) ? "" : $" [AUTH:{auth}]");
        Logger.Log("CAPTURE " + line);
        try
        {
            File.AppendAllText(AppPaths.CaptureFile, line + "\r\n");
        }
        catch
        {
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_kind == LoginKind.ChatGpt)
            {
                await SaveChatGptAsync();
            }
            else
            {
                await SaveZenAsync();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "保存失败: " + ex.Message;
            Logger.Log("LoginWindow save error: " + ex);
        }
    }

    private async Task SaveChatGptAsync()
    {
        var cookies = await _web.CoreWebView2.CookieManager.GetCookiesAsync("https://chatgpt.com");
        var parts = new List<string>();
        foreach (var cookie in cookies)
        {
            if (!string.IsNullOrEmpty(cookie.Name))
            {
                parts.Add($"{cookie.Name}={cookie.Value}");
            }
        }

        if (parts.Count == 0)
        {
            _statusLabel.Text = "没有拿到 cookie —— 请确认已经登录成功再试。";
            return;
        }

        CredentialStore.Save(CredentialTargets.ChatGptCookies, string.Join("; ", parts));
        _statusLabel.Text = $"已保存 {parts.Count} 个 cookie，可以关闭窗口了。";
        Logger.Log($"LOGIN chatgpt saved {parts.Count} cookies");
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task SaveZenAsync()
    {
        var currentUrl = _web.CoreWebView2.Source;
        var workspaceMatch = System.Text.RegularExpressions.Regex.Match(currentUrl, @"/workspace/(wrk_[^/]+)");
        if (!workspaceMatch.Success)
        {
            _statusLabel.Text = $"没找到 workspace 页面 (当前: {currentUrl})。请进入 opencode.ai 的 workspace 用量页后再保存。";
            return;
        }

        var workspaceId = workspaceMatch.Groups[1].Value;
        try
        {
            var settings = Settings.Load();
            settings.GoWorkspaceId = workspaceId;
            settings.Save();
        }
        catch (Exception ex)
        {
            Logger.Log("zen workspace save error: " + ex.Message);
        }

        CredentialStore.Save(CredentialTargets.ZenJwt, "ok:" + workspaceId);
        Logger.Log($"LOGIN zen saved workspace={workspaceId} url={currentUrl}");
        _statusLabel.Text = $"已保存 Zen 登录态 (workspace {workspaceId})，可以关闭窗口了。";
        DialogResult = DialogResult.OK;
        Close();
    }
}
