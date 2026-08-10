using System.Text.Json;
using QuotaTray.Infra;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace QuotaTray.Sources;

internal sealed record FetchResult(int Status, string Body);

internal sealed class HiddenFetchWebView : IDisposable
{
    private readonly string _profileName;
    private readonly string _entryUrl;
    private Form? _form;
    private WebView2? _web;
    private CoreWebView2Environment? _env;
    private TaskCompletionSource<FetchResult>? _pending;
    private bool _ready;
    private bool _navFailed;
    private SynchronizationContext? _uiContext;

    public HiddenFetchWebView(string profileName, string entryUrl)
    {
        _profileName = profileName;
        _entryUrl = entryUrl;
    }

    public Task<FetchResult> FetchAsync(string url, string headersJs, CancellationToken ct)
    {
        _uiContext ??= SynchronizationContext.Current;
        if (_uiContext is null)
        {
            return Task.FromResult(new FetchResult(0, "no UI context"));
        }

        return DispatchAsync(() => FetchAsyncCore(url, headersJs, ct));
    }

    private async Task<FetchResult> FetchAsyncCore(string url, string headersJs, CancellationToken ct)
    {
        await EnsureReadyAsync();
        if (_navFailed)
        {
            return new FetchResult(0, "webview 导航失败，请重新登录");
        }

        // 修复竞态：若上一次请求未完成，先完成它，避免旧 tcs 永不完成
        var previous = _pending;
        if (previous is not null)
        {
            previous.TrySetResult(new FetchResult(0, "superseded"));
            _pending = null;
        }

        var tcs = new TaskCompletionSource<FetchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = tcs;

        var js = $@"(async () => {{
            try {{
                let headers = {headersJs};
                try {{
                    if (location.origin === 'https://chatgpt.com') {{
                        const s = await fetch('/api/auth/session', {{ credentials: 'include' }});
                        const j = await s.json();
                        if (j.accessToken) headers['Authorization'] = 'Bearer ' + j.accessToken;
                    }}
                }} catch (e) {{ }}
                const r = await fetch({JsonSerializer.Serialize(url)}, {{ credentials: 'include', headers: headers }});
                const t = await r.text();
                window.chrome.webview.postMessage({{ status: r.status, body: t }});
            }} catch (e) {{
                window.chrome.webview.postMessage({{ status: 0, body: String(e) }});
            }}
        }})()";

        await _web!.CoreWebView2.ExecuteScriptAsync(js);

        using var registration = ct.Register(() => tcs.TrySetResult(new FetchResult(0, "timeout")));
        return await tcs.Task;
    }

    private async Task<T> DispatchAsync<T>(Func<Task<T>> action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            return await action();
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext!.Post(async _ =>
        {
            try
            {
                tcs.TrySetResult(await action());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, null);
        return await tcs.Task;
    }

    private async Task EnsureReadyAsync()
    {
        if (_ready)
        {
            return;
        }

        _navFailed = false;
        Exception? lastError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await EnsureReadyCoreAsync();
                _ready = true;
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Logger.Log($"HiddenFetch nav '{_profileName}' attempt {attempt + 1} failed: {ex.Message}");
                // 第一次失败时清理已创建的 web/form，下次循环重建
                if (attempt == 0)
                {
                    CleanupControls();
                    await Task.Delay(2000);
                }
            }
        }

        _navFailed = true;
        Logger.Log($"HiddenFetch nav '{_profileName}' giving up after retries: {lastError?.Message}");
    }

    private async Task EnsureReadyCoreAsync()
    {
        _form = new Form
        {
            ShowInTaskbar = false,
            Width = 32,
            Height = 32,
        };
        _form.Opacity = 0;
        _form.ShowInTaskbar = false;

        _web = new WebView2 { Dock = DockStyle.Fill };
        _form.Controls.Add(_web);
        _form.CreateControl();

        var profileDir = Path.Combine(AppPaths.WebViewDataDir, _profileName);
        _env = await CoreWebView2Environment.CreateAsync(null, profileDir);
        await _web.EnsureCoreWebView2Async(_env);
        _web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var navigation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _web.CoreWebView2.NavigationCompleted += (_, args) =>
        {
            Logger.Log($"HiddenFetch nav '{_profileName}' complete success={args.IsSuccess} status={args.HttpStatusCode}");
            if (args.IsSuccess)
            {
                navigation.TrySetResult();
            }
            else
            {
                navigation.TrySetException(new InvalidOperationException($"导航失败: {args.WebErrorStatus}"));
            }
        };

        Logger.Log($"HiddenFetch nav '{_profileName}' -> {_entryUrl}");
        _web.CoreWebView2.Navigate(_entryUrl);
        await navigation.Task.WaitAsync(TimeSpan.FromSeconds(40));
        Logger.Log($"HiddenFetch nav '{_profileName}' ready");
    }

    private void CleanupControls()
    {
        if (_web is not null)
        {
            try
            {
                _web.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _web.Dispose();
            }
            catch
            {
            }
        }

        try
        {
            _form?.Dispose();
        }
        catch
        {
        }

        _web = null;
        _form = null;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var pending = _pending;
        _pending = null;
        if (pending is null)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var status = doc.RootElement.GetProperty("status").GetInt32();
            var body = doc.RootElement.GetProperty("body").GetString() ?? "";
            pending.TrySetResult(new FetchResult(status, body));
        }
        catch (Exception ex)
        {
            pending.TrySetResult(new FetchResult(0, "js message parse: " + ex.Message));
        }
    }

    public void Dispose()
    {
        CleanupControls();
        _env = null;
        _ready = false;
        _navFailed = false;
        _pending = null;
    }
}
