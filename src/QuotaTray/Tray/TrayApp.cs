using QuotaTray.Auth;
using QuotaTray.Infra;
using QuotaTray.Model;
using QuotaTray.Sources;

namespace QuotaTray.Tray;

internal sealed class TrayApp : ApplicationContext
{
    private const string AutostartRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutostartName = "QuotaTray";

    private readonly NotifyIcon _icon = new();
    private readonly TooltipForm _tip = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _hoverTimer;
    private readonly System.Windows.Forms.Timer _hoverWatchTimer;
    private readonly Control _syncControl = new();
    private readonly SynchronizationContext? _postCtx;
    private readonly ChatGptUsage _chat = new();
    private readonly GoUsage _go = new();
    private readonly Settings _settings = Settings.Load();

    private UsageSnapshot _snapshot = new();
    private bool _refreshing;
    private Point _lastIconPos = Point.Empty;

    public TrayApp()
    {
        _postCtx = SynchronizationContext.Current;
        EnsureAutostart();
        BuildMenu();

        _icon.Icon = IconFactory.Get(IconFactory.Gray);
        _icon.Visible = true;
        _icon.Text = "";

        _hoverTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            _tip.ShowAt(Cursor.Position, _snapshot.TooltipText);
            _hoverWatchTimer.Start();
        };

        _hoverWatchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _hoverWatchTimer.Tick += (_, _) =>
        {
            var pos = Cursor.Position;
            var insideTip = _tip.Visible && _tip.Bounds.Contains(pos);
            var nearIcon = Math.Abs(pos.X - _lastIconPos.X) <= 60 && Math.Abs(pos.Y - _lastIconPos.Y) <= 60;
            if (!insideTip && !nearIcon)
            {
                _hoverWatchTimer.Stop();
                _tip.Hide();
            }
        };

        _icon.MouseMove += (_, _) =>
        {
            _lastIconPos = Cursor.Position;
            if (!_tip.Visible)
            {
                _hoverTimer.Stop();
                _hoverTimer.Start();
            }
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _hoverTimer.Stop();
                _tip.Hide();
            }
        };

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, _settings.RefreshIntervalMinutes) * 60 * 1000,
        };
        _refreshTimer.Tick += async (_, _) => await RefreshNowAsync();
        _refreshTimer.Start();

        _ = RefreshNowAsync();

        RunFirstLoginIfNeeded();
    }

    private void BuildMenu()
    {
        var refresh = new ToolStripMenuItem("立即刷新");
        refresh.Click += async (_, _) => await RefreshNowAsync();

        var loginChat = new ToolStripMenuItem("重新登录 ChatGPT...");
        loginChat.Click += (_, _) => ShowLogin(LoginKind.ChatGpt);

        var loginZen = new ToolStripMenuItem("重新登录 Go...");
        loginZen.Click += (_, _) => ShowLogin(LoginKind.Zen);

        var autostart = new ToolStripMenuItem("开机自启") { Checked = IsAutostartEnabled() };
        autostart.Click += (_, _) => SetAutostart(!autostart.Checked);

        var openData = new ToolStripMenuItem("打开数据目录");
        openData.Click += (_, _) => System.Diagnostics.Process.Start("explorer.exe", AppPaths.DataDir);

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => Exit();

        _menu.Items.AddRange(new ToolStripItem[]
        {
            refresh, new ToolStripSeparator(),
            loginChat, loginZen, new ToolStripSeparator(),
            autostart, openData, new ToolStripSeparator(),
            exit,
        });

        _icon.ContextMenuStrip = _menu;
    }

    private async void ShowLogin(LoginKind kind)
    {
        if (kind == LoginKind.ChatGpt)
        {
            _chat.ResetSession();
        }
        else
        {
            _go.ResetSession();
        }

        using (var window = new LoginWindow(kind))
        {
            window.ShowDialog();
        }

        await Task.Delay(300);
        await RefreshNowAsync();
    }

    private void RunFirstLoginIfNeeded()
    {
        var hasChatGpt = CredentialStore.Read(CredentialTargets.ChatGptCookies) != null;
        var hasZen = CredentialStore.Read(CredentialTargets.ZenJwt) != null;

        if (!hasChatGpt || !hasZen)
        {
            _icon.ShowBalloonTip(8000, "Agent Usage Checker",
                "首次使用，请完成登录：\nChatGPT（用量查询）" + (hasZen ? "" : "\nZen / opencode Go（用量查询）"),
                ToolTipIcon.Info);
        }

        if (!hasChatGpt)
        {
            ShowLogin(LoginKind.ChatGpt);
        }

        if (!hasZen && CredentialStore.Read(CredentialTargets.ChatGptCookies) != null)
        {
            ShowLogin(LoginKind.Zen);
        }
    }

    private async Task RefreshNowAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var settings = Settings.Load();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            var snapshot = new UsageSnapshot();

            var chatTask = _chat.FetchAsync(settings.ChatGptEndpoint ?? "", settings.ChatGptJsonPath,
                settings.ChatGptMaxValue, settings.ChatGptValueIsRemaining, cts.Token);

            var goTask = _go.FetchAsync(settings.GoWorkspaceId ?? "", cts.Token);

            await Task.WhenAll(chatTask, goTask);

            (snapshot.ChatGptStatus, snapshot.ChatGptDetail, snapshot.ChatGptPercent,
                snapshot.ChatGptResetSec) = chatTask.Result;
            (snapshot.GoStatus, snapshot.GoDetail, snapshot.Go5hPercent, snapshot.GoWeekPercent,
                snapshot.GoMonthPercent, snapshot.GoReset5hSec, snapshot.GoResetWeekSec,
                snapshot.GoResetMonthSec) = goTask.Result;

            _snapshot = snapshot;
            Logger.Log($"REFRESH ok: chat={snapshot.ChatGptStatus}/{snapshot.ChatGptDetail} go={snapshot.GoStatus}/{snapshot.GoDetail}");
            _postCtx?.Post(_ => UpdateUi(), null);
        }
        catch (Exception ex)
        {
            Logger.Log("refresh error: " + ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateUi()
    {
        var s = _snapshot;
        Color color;
        if (s.HasError || s.OverallPercent is null)
        {
            color = IconFactory.Gray;
        }
        else if (s.OverallPercent < 20)
        {
            color = IconFactory.Red;
        }
        else if (s.OverallPercent < 50)
        {
            color = IconFactory.Yellow;
        }
        else
        {
            color = IconFactory.Green;
        }

        _icon.Icon = IconFactory.Get(color);
        _icon.Text = "";
    }

    private static void EnsureAutostart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AutostartRunKey);
            key.DeleteValue("AgentUsageChecker", false);
        }
        catch
        {
        }

        if (!IsAutostartEnabled())
        {
            SetAutostart(true);
        }
    }

    private static bool IsAutostartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutostartRunKey);
            return key?.GetValue(AutostartName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAutostart(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AutostartRunKey);
            if (enabled)
            {
                key.SetValue(AutostartName, "\"" + Application.ExecutablePath + "\"");
            }
            else
            {
                key.DeleteValue(AutostartName, false);
            }
        }
        catch (Exception ex)
        {
            Logger.Log("autostart error: " + ex.Message);
        }
    }

    private void Exit()
    {
        _hoverTimer.Stop();
        _hoverWatchTimer.Stop();
        _refreshTimer.Stop();
        _tip.Hide();
        _tip.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        ExitThread();
    }
}
